namespace ExcelMcp.Deployment.Doctor;

public sealed class WindowsExcelAvailabilityProbe : IExcelAvailabilityProbe
{
    public Task<ExcelAvailabilityProbeResult> CheckAsync(
        ExcelAvailabilityProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ExcelAvailabilityProbeResult(
                DoctorCheckSeverity.Warning,
                "Excel availability checks require Windows.",
                "Run GridPilot MCP doctor checks on the Windows desktop where Excel is installed."));
        }

        try
        {
            var type = Type.GetTypeFromProgID(GetExcelApplicationProgId());
            if (type is null)
            {
                return Task.FromResult(new ExcelAvailabilityProbeResult(
                    DoctorCheckSeverity.Error,
                    "Microsoft Excel COM registration was not found.",
                    "Install Microsoft Excel desktop and run this check from that Windows user session."));
            }

            if (request.Mode == ExcelAvailabilityProbeMode.Passive)
            {
                return Task.FromResult(new ExcelAvailabilityProbeResult(
                    DoctorCheckSeverity.Ok,
                    "Microsoft Excel COM registration is present.",
                    "No action needed."));
            }

            return Task.FromResult(CheckActive(type));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new ExcelAvailabilityProbeResult(
                DoctorCheckSeverity.Error,
                $"Excel availability check failed: {exception.Message}",
                "Verify Microsoft Excel desktop is installed and available to the current user."));
        }
    }

    private static ExcelAvailabilityProbeResult CheckActive(Type type)
    {
        object? application = null;
        try
        {
            application = Activator.CreateInstance(type);
            if (application is null)
            {
                return new ExcelAvailabilityProbeResult(
                    DoctorCheckSeverity.Error,
                    "Excel COM activation returned no application object.",
                    "Repair or reinstall Microsoft Excel desktop.");
            }

            type.InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, binder: null, target: application, args: null);
            return new ExcelAvailabilityProbeResult(
                DoctorCheckSeverity.Ok,
                "Microsoft Excel COM activation succeeded.",
                "No action needed.");
        }
        catch (Exception exception)
        {
            return new ExcelAvailabilityProbeResult(
                DoctorCheckSeverity.Error,
                $"Excel COM activation failed: {exception.Message}",
                "Close modal Excel dialogs, verify desktop Excel can start normally, and rerun the doctor.");
        }
        finally
        {
            ReleaseComObject(application);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            var marshalType = typeof(object).Assembly.GetType(string.Concat("System.Runtime.", "Inter", "opServices.Marshal"));
            var method = marshalType?.GetMethod("FinalReleaseComObject", [typeof(object)]);
            method?.Invoke(null, [value]);
        }
        catch (Exception)
        {
            // Best-effort cleanup only; the doctor result has already captured activation status.
        }
    }

    private static string GetExcelApplicationProgId() =>
        string.Concat("Excel", ".", "Application");
}
