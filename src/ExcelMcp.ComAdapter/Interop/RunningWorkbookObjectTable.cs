using ExcelMcp.Core;
using ExcelMcp.Core.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal static class RunningWorkbookObjectTable
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(10);
    private static readonly Guid IDispatchInterfaceId = new("00020400-0000-0000-C000-000000000046");
    private static readonly HashSet<string> WorkbookExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
        ".xlsm",
        ".xlsb",
        ".xltx",
        ".xltm",
        ".xlam",
        ".xla",
        ".csv"
    };

    public static IReadOnlyList<WorkbookSummary> ListRunningWorkbooks(IGridPilotLogger? logger = null) =>
        StaOperationRunner.Run(
            ListRunningWorkbooksCore,
            DiscoveryTimeout,
            "running_workbook_discovery_timeout",
            "Timed out while enumerating running Excel workbooks.",
            () => BuildDiscoveryTimeoutDetail(
                "The running object table did not return workbook discovery results within the allotted timeout."),
            logger);

    private static IReadOnlyList<WorkbookSummary> ListRunningWorkbooksCore()
    {
        var workbooks = new Dictionary<string, WorkbookSummary>(StringComparer.OrdinalIgnoreCase);

        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var runningObjectTable));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out var bindContext));
        IEnumMoniker? monikerEnumerator = null;

        try
        {
            runningObjectTable.EnumRunning(out monikerEnumerator);
            var monikers = new IMoniker[1];

            while (monikerEnumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                try
                {
                    if (!TryResolveWorkbookPath(moniker, bindContext, out _, out var normalizedPath))
                    {
                        continue;
                    }

                    runningObjectTable.GetObject(moniker, out var runningObject);
                    try
                    {
                        if (!TryReadWorkbookIdentity(runningObject, normalizedPath, out var workbookName, out var fullPath))
                        {
                            continue;
                        }

                        var isActive = TryIsActiveWorkbook(runningObject, fullPath);

                        if (workbooks.TryGetValue(fullPath, out var existing))
                        {
                            workbooks[fullPath] = existing with { IsActive = existing.IsActive || isActive };
                        }
                        else
                        {
                            workbooks[fullPath] = new WorkbookSummary(workbookName, fullPath, isActive);
                        }
                    }
                    finally
                    {
                        ComDispatch.ReleaseIfComObject(runningObject);
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(moniker);
                    monikers[0] = null!;
                }
            }

            return workbooks.Values
                .OrderBy(workbook => workbook.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(workbook => workbook.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(monikerEnumerator);
            ComDispatch.ReleaseIfComObject(bindContext);
            ComDispatch.ReleaseIfComObject(runningObjectTable);
        }
    }

    public static IReadOnlyList<object> FindWorkbookOwnerApplications(string workbookPath, IGridPilotLogger? logger = null)
    {
        var resolvedLogger = logger ?? GridPilotNullLogger.Instance;
        var normalizedWorkbookPath = NormalizePath(workbookPath);
        resolvedLogger.LogDebug(nameof(RunningWorkbookObjectTable), "find_workbook_owner_started", new Dictionary<string, object?>
        {
            ["workbookPath"] = normalizedWorkbookPath
        });

        var marshaledApplicationStreams = FindWorkbookOwnerApplicationStreams(workbookPath, resolvedLogger);
        var applications = ResolveOwnerApplicationsFromStreams(marshaledApplicationStreams);
        resolvedLogger.LogDebug(nameof(RunningWorkbookObjectTable), "find_workbook_owner_finished", new Dictionary<string, object?>
        {
            ["workbookPath"] = normalizedWorkbookPath,
            ["matchingApplicationStreamCount"] = marshaledApplicationStreams.Count,
            ["matchCount"] = applications.Count
        });

        return applications;
    }

    private static string BuildDiscoveryTimeoutDetail(string baseDetail)
    {
        var processes = CaptureExcelProcesses();
        if (processes.Length == 0)
        {
            return $"{baseDetail} No running EXCEL.EXE processes were visible from the current user session.";
        }

        var snapshot = string.Join(
            "; ",
            processes.Select(process =>
                $"pid={process.Id}, title='{process.MainWindowTitle}', responding={process.Responding}, path='{process.ProcessPath}'"));

        return $"{baseDetail} Observed {processes.Length} EXCEL.EXE process(es): {snapshot}";
    }

    private static ExcelProcessSnapshot[] CaptureExcelProcesses()
    {
        try
        {
            return Process.GetProcessesByName("EXCEL")
                .Select(process =>
                {
                    try
                    {
                        return new ExcelProcessSnapshot(
                            process.Id,
                            string.IsNullOrWhiteSpace(process.MainWindowTitle) ? "<hidden>" : process.MainWindowTitle,
                            SafeReadProcessPath(process),
                            SafeReadResponding(process));
                    }
                    finally
                    {
                        process.Dispose();
                    }
                })
                .OrderBy(process => process.Id)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string SafeReadProcessPath(Process process)
    {
        try
        {
            return string.IsNullOrWhiteSpace(process.MainModule?.FileName)
                ? "<unavailable>"
                : process.MainModule.FileName;
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static bool? SafeReadResponding(Process process)
    {
        try
        {
            return process.Responding;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ExcelProcessSnapshot(int Id, string MainWindowTitle, string ProcessPath, bool? Responding);

    private static IReadOnlyList<nint> FindWorkbookOwnerApplicationStreams(string workbookPath, IGridPilotLogger logger) =>
        StaOperationRunner.Run(
            () => FindWorkbookOwnerApplicationStreamsCore(workbookPath),
            DiscoveryTimeout,
            "running_workbook_discovery_timeout",
            "Timed out while locating the running Excel workbook owner.",
            () => BuildDiscoveryTimeoutDetail(
                $"The running object table did not return workbook-owner matches for '{NormalizePath(workbookPath)}' within the allotted timeout."),
            logger);

    private static IReadOnlyList<nint> FindWorkbookOwnerApplicationStreamsCore(string workbookPath)
    {
        var normalizedTarget = NormalizePath(workbookPath);
        var marshaledStreamsByIdentity = new Dictionary<nint, nint>();

        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var runningObjectTable));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out var bindContext));
        IEnumMoniker? monikerEnumerator = null;

        try
        {
            runningObjectTable.EnumRunning(out monikerEnumerator);
            var monikers = new IMoniker[1];

            while (monikerEnumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                try
                {
                    if (!TryResolveWorkbookPath(moniker, bindContext, out _, out var candidatePath))
                    {
                        continue;
                    }

                    runningObjectTable.GetObject(moniker, out var runningObject);
                    try
                    {
                        if (!TryReadWorkbookIdentity(runningObject, candidatePath, out _, out var resolvedWorkbookPath) ||
                            !WorkbookPathMatchesTarget(normalizedTarget, candidatePath, resolvedWorkbookPath))
                        {
                            continue;
                        }

                        var application = ComDispatch.GetProperty<object>(runningObject, "Application");
                        try
                        {
                            var identity = GetComIdentity(application);
                            if (!marshaledStreamsByIdentity.ContainsKey(identity))
                            {
                                var interfaceId = IDispatchInterfaceId;
                                Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(
                                    ref interfaceId,
                                    application,
                                    out var marshaledStream));
                                marshaledStreamsByIdentity[identity] = marshaledStream;
                            }
                        }
                        finally
                        {
                            ComDispatch.ReleaseIfComObject(application);
                        }
                    }
                    finally
                    {
                        ComDispatch.ReleaseIfComObject(runningObject);
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(moniker);
                    monikers[0] = null!;
                }
            }

            return marshaledStreamsByIdentity.Values.ToArray();
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(monikerEnumerator);
            ComDispatch.ReleaseIfComObject(bindContext);
            ComDispatch.ReleaseIfComObject(runningObjectTable);
        }
    }

    private static IReadOnlyList<object> ResolveOwnerApplicationsFromStreams(IReadOnlyCollection<nint> marshaledApplicationStreams)
    {
        if (marshaledApplicationStreams.Count == 0)
        {
            return [];
        }

        var applications = new List<object>(marshaledApplicationStreams.Count);
        var remainingStreams = new List<nint>(marshaledApplicationStreams);

        try
        {
            foreach (var marshaledStream in marshaledApplicationStreams)
            {
                var interfaceId = IDispatchInterfaceId;
                Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(
                    marshaledStream,
                    ref interfaceId,
                    out var application));
                applications.Add(application);
                remainingStreams.Remove(marshaledStream);
            }

            return applications;
        }
        finally
        {
            foreach (var marshaledStream in remainingStreams)
            {
                try
                {
                    Marshal.Release(marshaledStream);
                }
                catch
                {
                }
            }
        }
    }

    internal static bool WorkbookPathMatchesTarget(string targetPath, string candidatePath, string resolvedWorkbookPath)
    {
        var normalizedTarget = NormalizePath(targetPath);
        var normalizedCandidate = NormalizePath(candidatePath);
        var normalizedResolved = NormalizePath(resolvedWorkbookPath);

        return string.Equals(normalizedCandidate, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedResolved, normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static nint GetComIdentity(object comObject)
    {
        var unknown = Marshal.GetIUnknownForObject(comObject);
        try
        {
            return unknown;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private static bool TryResolveWorkbookPath(IMoniker moniker, IBindCtx bindContext, out string rawDisplayName, out string normalizedPath)
    {
        rawDisplayName = string.Empty;
        normalizedPath = string.Empty;
        if (!TryGetMonikerDisplayName(moniker, bindContext, out rawDisplayName))
        {
            return false;
        }

        return TryNormalizeWorkbookCandidatePath(rawDisplayName, out normalizedPath);
    }

    private static bool TryGetMonikerDisplayName(IMoniker moniker, IBindCtx bindContext, out string displayName)
    {
        displayName = string.Empty;

        try
        {
            moniker.GetDisplayName(bindContext, null, out displayName);
            return !string.IsNullOrWhiteSpace(displayName);
        }
        catch (COMException)
        {
            return false;
        }
    }

    internal static bool TryNormalizeWorkbookCandidatePath(string displayName, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        var candidate = displayName.Trim();
        if (candidate.StartsWith("!", StringComparison.Ordinal))
        {
            candidate = candidate[1..];
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            normalizedPath = NormalizePath(candidate);
        }
        catch (Exception)
        {
            return false;
        }

        return LooksLikeWorkbookPath(normalizedPath);
    }

    internal static bool LooksLikeWorkbookPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) && WorkbookExtensions.Contains(extension);
    }

    private static bool TryReadWorkbookIdentity(object runningObject, string fallbackPath, out string workbookName, out string fullPath)
    {
        workbookName = string.Empty;
        fullPath = string.Empty;

        var resolvedPath = TryReadWorkbookFullName(runningObject) ?? fallbackPath;
        if (!LooksLikeWorkbookPath(resolvedPath))
        {
            return false;
        }

        if (!IsExcelWorkbookObject(runningObject))
        {
            return false;
        }

        workbookName = TryReadWorkbookName(runningObject) ?? Path.GetFileName(resolvedPath);
        fullPath = resolvedPath;
        return true;
    }

    private static bool IsExcelWorkbookObject(object runningObject)
    {
        var workbookName = TryReadWorkbookName(runningObject);
        if (string.IsNullOrWhiteSpace(workbookName))
        {
            return false;
        }

        object? application = null;
        try
        {
            application = ComDispatch.GetProperty<object>(runningObject, "Application");
            var applicationName = ComDispatch.GetProperty<string>(application, "Name");
            return string.Equals(applicationName, "Microsoft Excel", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(application);
        }
    }

    private static string? TryReadWorkbookName(object workbook)
    {
        try
        {
            return ComDispatch.GetProperty<string>(workbook, "Name");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadWorkbookFullName(object workbook)
    {
        try
        {
            return NormalizePath(ComDispatch.GetProperty<string>(workbook, "FullName"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryIsActiveWorkbook(object workbook, string workbookPath)
    {
        object? application = null;
        object? activeWorkbook = null;
        try
        {
            application = ComDispatch.GetProperty<object>(workbook, "Application");
            activeWorkbook = ComDispatch.GetProperty<object?>(application, "ActiveWorkbook");
            if (activeWorkbook is null)
            {
                return false;
            }

            var activePath = TryReadWorkbookFullName(activeWorkbook);
            return activePath is not null &&
                   string.Equals(activePath, workbookPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(activeWorkbook);
            ComDispatch.ReleaseIfComObject(application);
        }
    }

    internal static string NormalizePath(string workbookPath)
    {
        return WorkbookIdentity.Normalize(workbookPath);
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable runningObjectTable);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx bindContext);

    [DllImport("ole32.dll")]
    private static extern int CoMarshalInterThreadInterfaceInStream(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] object unk, out nint stream);

    [DllImport("ole32.dll")]
    private static extern int CoGetInterfaceAndReleaseStream(nint stream, ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object unk);
}
