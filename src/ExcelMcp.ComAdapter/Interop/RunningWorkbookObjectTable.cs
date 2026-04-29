using ExcelMcp.Core;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal static class RunningWorkbookObjectTable
{
    public static IReadOnlyList<object> FindWorkbookOwnerApplications(string workbookPath)
    {
        var normalizedTarget = NormalizePath(workbookPath);
        var applicationsByIdentity = new Dictionary<nint, object>();

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
                    if (!TryGetMatchingWorkbookPath(moniker, bindContext, normalizedTarget))
                    {
                        continue;
                    }

                    runningObjectTable.GetObject(moniker, out var runningObject);
                    try
                    {
                        var application = ComDispatch.GetProperty<object>(runningObject, "Application");
                        var identity = GetComIdentity(application);
                        if (!applicationsByIdentity.TryAdd(identity, application))
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

            return applicationsByIdentity.Values.ToArray();
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(monikerEnumerator);
            ComDispatch.ReleaseIfComObject(bindContext);
            ComDispatch.ReleaseIfComObject(runningObjectTable);
        }
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

    private static bool TryGetMatchingWorkbookPath(IMoniker moniker, IBindCtx bindContext, string normalizedTarget)
    {
        string displayName;
        try
        {
            moniker.GetDisplayName(bindContext, null, out displayName);
        }
        catch (COMException)
        {
            return false;
        }

        var candidate = displayName.Trim();
        if (candidate.StartsWith("!", StringComparison.Ordinal))
        {
            candidate = candidate[1..];
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string normalizedCandidate;
        try
        {
            normalizedCandidate = NormalizePath(candidate);
        }
        catch (Exception)
        {
            return false;
        }

        return string.Equals(normalizedCandidate, normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string workbookPath) => Path.GetFullPath(workbookPath);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable runningObjectTable);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx bindContext);
}
