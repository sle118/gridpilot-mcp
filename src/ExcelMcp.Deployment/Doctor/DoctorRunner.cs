using System.Text.Json;
using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.Doctor;

public sealed class DoctorRunner
{
    private readonly IExcelAvailabilityProbe _excelProbe;
    private readonly IRuntimeConfigReader _runtimeConfigReader;
    private readonly IWritableDirectoryProbe _writableDirectoryProbe;

    public DoctorRunner(
        IExcelAvailabilityProbe? excelProbe = null,
        IRuntimeConfigReader? runtimeConfigReader = null,
        IWritableDirectoryProbe? writableDirectoryProbe = null)
    {
        _excelProbe = excelProbe ?? new WindowsExcelAvailabilityProbe();
        _runtimeConfigReader = runtimeConfigReader ?? new RuntimeConfigReader();
        _writableDirectoryProbe = writableDirectoryProbe ?? new WritableDirectoryProbe();
    }

    public async Task<DoctorReport> RunAsync(
        string profilePath,
        DoctorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DoctorOptions();
        var results = new List<DoctorCheckResult>();

        var profile = CheckProfile(profilePath, results);
        if (profile is not null)
        {
            RunProfileChecks(profile, options, results);
        }

        await AddExcelProbeResultAsync(options, results, cancellationToken).ConfigureAwait(false);

        return new DoctorReport(results);
    }

    private static LaunchProfile? CheckProfile(string profilePath, List<DoctorCheckResult> results)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            results.Add(Error(
                "profile.path",
                "Profile path",
                "Launch profile path is required.",
                "Pass the path to a GridPilot launch profile JSON file."));
            return null;
        }

        if (!File.Exists(profilePath))
        {
            results.Add(Error(
                "profile.exists",
                "Profile file",
                $"Launch profile file '{profilePath}' does not exist.",
                "Create or select a valid GridPilot launch profile file."));
            return null;
        }

        results.Add(Ok("profile.exists", "Profile file", $"Launch profile file '{profilePath}' exists."));

        var load = LaunchProfileLoader.Load(profilePath);
        if (load.Profile is null)
        {
            foreach (var issue in load.Issues)
            {
                results.Add(FromProfileIssue($"profile.load.{issue.Code}", "Profile load", issue));
            }

            return null;
        }

        results.Add(Ok("profile.load", "Profile load", "Launch profile loaded successfully."));

        var validation = LaunchProfileValidator.Validate(load.Profile);
        if (validation.IsValid)
        {
            results.Add(Ok("profile.validation", "Profile validation", "Launch profile is valid."));
        }
        else
        {
            foreach (var issue in validation.Issues)
            {
                results.Add(FromProfileIssue($"profile.validation.{issue.Code}", "Profile validation", issue));
            }
        }

        return load.Profile;
    }

    private void RunProfileChecks(LaunchProfile profile, DoctorOptions options, List<DoctorCheckResult> results)
    {
        SafeAdd(results, () => CheckHostCommand(profile));
        SafeAdd(results, () => CheckRuntimeConfig(profile));
        SafeAdd(results, () => CheckWorkingDirectory(profile));
        SafeAdd(results, () => CheckStdoutPolicy(profile));
        SafeAddRange(results, () => CheckLogs(profile, options));
    }

    private static DoctorCheckResult CheckHostCommand(LaunchProfile profile)
    {
        var command = profile.Host?.Command;
        if (string.IsNullOrWhiteSpace(command))
        {
            return Error(
                "host.command",
                "Host executable",
                "Launch profile host.command is missing.",
                "Set host.command to the built GridPilot MCP host executable.");
        }

        if (!File.Exists(command))
        {
            return Error(
                "host.command",
                "Host executable",
                $"Host executable '{command}' does not exist.",
                "Build the host project or update the launch profile command path.");
        }

        var attributes = File.GetAttributes(command);
        if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            return Error(
                "host.command",
                "Host executable",
                $"Host command '{command}' is a directory, not a file.",
                "Set host.command to the host executable file.");
        }

        return Ok("host.command", "Host executable", $"Host executable '{command}' exists.");
    }

    private DoctorCheckResult CheckRuntimeConfig(LaunchProfile profile)
    {
        var command = profile.Host?.Command;
        if (string.IsNullOrWhiteSpace(command) || !File.Exists(command))
        {
            return Warning(
                "host.runtimeconfig",
                "Host runtimeconfig",
                "Runtimeconfig could not be checked because the host command is missing.",
                "Fix host.command first, then rerun the doctor.");
        }

        var runtimeConfigPath = GetRuntimeConfigPath(command);
        if (!File.Exists(runtimeConfigPath))
        {
            return Warning(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host runtimeconfig '{runtimeConfigPath}' was not found.",
                "Build the host project and verify the runtimeconfig file is next to the executable.");
        }

        RuntimeConfigInfo info;
        try
        {
            info = _runtimeConfigReader.Read(runtimeConfigPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Error(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host runtimeconfig '{runtimeConfigPath}' could not be read: {exception.Message}",
                "Rebuild the host project so a valid runtimeconfig file is generated.");
        }

        if (string.IsNullOrWhiteSpace(info.FrameworkName) || string.IsNullOrWhiteSpace(info.FrameworkVersion))
        {
            return Error(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host runtimeconfig '{runtimeConfigPath}' does not declare a framework name and version.",
                "Rebuild the host project so a valid runtimeconfig file is generated.");
        }

        if (!string.Equals(info.FrameworkName, "Microsoft.NETCore.App", StringComparison.Ordinal))
        {
            return Warning(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host runtimeconfig framework is '{info.FrameworkName}'.",
                "Verify the host targets Microsoft.NETCore.App before deployment.");
        }

        if (!Version.TryParse(info.FrameworkVersion, out var frameworkVersion))
        {
            return Error(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host runtimeconfig framework version '{info.FrameworkVersion}' is invalid.",
                "Rebuild the host project so a valid runtimeconfig file is generated.");
        }

        if (frameworkVersion.Major > Environment.Version.Major)
        {
            return Warning(
                "host.runtimeconfig",
                "Host runtimeconfig",
                $"Host targets .NET {frameworkVersion.Major}, while the doctor is running on .NET {Environment.Version.Major}.",
                "Install a compatible .NET runtime for the host or run the doctor with a matching runtime.");
        }

        return Ok(
            "host.runtimeconfig",
            "Host runtimeconfig",
            info.UsesIncludedFrameworks
                ? $"Host runtimeconfig includes {info.FrameworkName} {info.FrameworkVersion} for a self-contained deployment."
                : $"Host runtimeconfig targets {info.FrameworkName} {info.FrameworkVersion}.");
    }

    private static DoctorCheckResult CheckWorkingDirectory(LaunchProfile profile)
    {
        var workingDirectory = profile.Host?.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Ok("host.workingDirectory", "Working directory", "No host working directory is configured.");
        }

        if (!Directory.Exists(workingDirectory))
        {
            return Error(
                "host.workingDirectory",
                "Working directory",
                $"Host working directory '{workingDirectory}' does not exist.",
                "Create the directory or update host.workingDirectory in the launch profile.");
        }

        return Ok("host.workingDirectory", "Working directory", $"Host working directory '{workingDirectory}' exists.");
    }

    private static DoctorCheckResult CheckStdoutPolicy(LaunchProfile profile)
    {
        if (string.Equals(profile.Logs?.StdoutPolicy, LaunchProfileValidator.JsonRpcOnlyStdoutPolicy, StringComparison.Ordinal))
        {
            return Ok(
                "logs.stdoutPolicy",
                "MCP stdout policy",
                "MCP stdout policy is jsonRpcOnly.");
        }

        return Error(
            "logs.stdoutPolicy",
            "MCP stdout policy",
            "MCP stdout policy is not jsonRpcOnly.",
            "Set logs.stdoutPolicy to jsonRpcOnly so diagnostics cannot pollute MCP stdout.");
    }

    private IReadOnlyList<DoctorCheckResult> CheckLogs(LaunchProfile profile, DoctorOptions options)
    {
        var results = new List<DoctorCheckResult>();
        var logs = DeploymentLogLocator.Locate(profile, options.CurrentDirectory);
        results.Add(Ok(
            "logs.candidates",
            "Log candidates",
            $"Discovered {logs.Count} runtime/proxy log candidate path(s)."));

        foreach (var log in logs)
        {
            var logName = $"Log file ({log.Kind})";
            var message = log.Exists
                ? $"Log candidate '{log.Path}' exists with access status {log.AccessStatus}."
                : $"Log candidate '{log.Path}' does not exist yet.";
            results.Add(Ok($"logs.candidate.{log.Kind}", logName, message));
        }

        var directories = logs
            .Select(log => Path.GetDirectoryName(log.Path))
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var directory in directories)
        {
            results.Add(_writableDirectoryProbe.CheckWritable(
                directory!,
                $"logs.directory.{Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(directory!))}",
                "Log directory writable"));
        }

        return results;
    }

    private async Task AddExcelProbeResultAsync(
        DoctorOptions options,
        List<DoctorCheckResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            var mode = options.AllowActiveExcelComProbe
                ? ExcelAvailabilityProbeMode.Active
                : ExcelAvailabilityProbeMode.Passive;
            var probe = await _excelProbe.CheckAsync(new ExcelAvailabilityProbeRequest(mode), cancellationToken)
                .ConfigureAwait(false);

            results.Add(new DoctorCheckResult(
                "excel.availability",
                "Excel availability",
                probe.Severity,
                probe.Message,
                probe.SuggestedNextStep));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            results.Add(Error(
                "excel.availability",
                "Excel availability",
                $"Excel availability probe failed: {exception.Message}",
                "Verify Microsoft Excel desktop is installed and rerun the doctor."));
        }
    }

    private static void SafeAdd(List<DoctorCheckResult> results, Func<DoctorCheckResult> check)
    {
        try
        {
            results.Add(check());
        }
        catch (Exception exception)
        {
            results.Add(Error(
                "doctor.checkFailed",
                "Doctor check failed",
                $"A doctor check failed unexpectedly: {exception.Message}",
                "Collect the diagnostic report and inspect the deployment profile."));
        }
    }

    private static void SafeAddRange(List<DoctorCheckResult> results, Func<IReadOnlyList<DoctorCheckResult>> check)
    {
        try
        {
            results.AddRange(check());
        }
        catch (Exception exception)
        {
            results.Add(Error(
                "doctor.checkFailed",
                "Doctor check failed",
                $"A doctor check failed unexpectedly: {exception.Message}",
                "Collect the diagnostic report and inspect the deployment profile."));
        }
    }

    private static DoctorCheckResult FromProfileIssue(string id, string name, LaunchProfileIssue issue) =>
        new(
            id,
            name,
            issue.Severity == LaunchProfileIssueSeverity.Error ? DoctorCheckSeverity.Error : DoctorCheckSeverity.Warning,
            issue.Message,
            "Fix the launch profile JSON and rerun the doctor.");

    private static string GetRuntimeConfigPath(string command)
    {
        var directory = Path.GetDirectoryName(command);
        var fileName = Path.GetFileNameWithoutExtension(command) + ".runtimeconfig.json";
        return string.IsNullOrWhiteSpace(directory)
            ? fileName
            : Path.Combine(directory, fileName);
    }

    private static DoctorCheckResult Ok(string id, string name, string message) =>
        new(id, name, DoctorCheckSeverity.Ok, message, "No action needed.");

    private static DoctorCheckResult Warning(string id, string name, string message, string nextStep) =>
        new(id, name, DoctorCheckSeverity.Warning, message, nextStep);

    private static DoctorCheckResult Error(string id, string name, string message, string nextStep) =>
        new(id, name, DoctorCheckSeverity.Error, message, nextStep);
}
