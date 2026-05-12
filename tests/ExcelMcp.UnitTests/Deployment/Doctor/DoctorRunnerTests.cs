using System.Text.Json;
using ExcelMcp.Deployment.Doctor;

namespace ExcelMcp.UnitTests.Deployment.Doctor;

public sealed class DoctorRunnerTests
{
    [Fact]
    public async Task RunAsync_ValidProfileProducesMostlyOkStaticChecks()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.False(report.HasErrors);
        Assert.Contains(report.Results, result => result.Id == "profile.validation" && result.Severity == DoctorCheckSeverity.Ok);
        Assert.Contains(report.Results, result => result.Id == "host.command" && result.Severity == DoctorCheckSeverity.Ok);
        Assert.Contains(report.Results, result => result.Id == "host.runtimeconfig" && result.Severity == DoctorCheckSeverity.Ok);
        Assert.Contains(report.Results, result => result.Id == "logs.stdoutPolicy" && result.Severity == DoctorCheckSeverity.Ok);
        Assert.Contains(report.Results, result => result.Id == "excel.availability" && result.Severity == DoctorCheckSeverity.Ok);
    }

    [Fact]
    public async Task RunAsync_MissingProfileFileProducesError()
    {
        using var temp = DoctorTestWorkspace.Create();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(Path.Combine(temp.DirectoryPath, "missing.json"));

        Assert.Contains(report.Results, result =>
            result.Id == "profile.exists" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_InvalidProfileJsonSurfacesLoadError()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = Path.Combine(temp.DirectoryPath, "profile.json");
        File.WriteAllText(profilePath, "{ invalid");
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "profile.load.profile_invalid_json" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_MissingHostCommandProducesValidationAndHostErrors()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile(commandPath: Path.Combine(temp.DirectoryPath, "missing.exe"));
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "profile.validation.host_command_not_found" &&
            result.Severity == DoctorCheckSeverity.Error);
        Assert.Contains(report.Results, result =>
            result.Id == "host.command" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_MissingRuntimeConfigProducesWarning()
    {
        using var temp = DoctorTestWorkspace.Create(writeRuntimeConfig: false);
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "host.runtimeconfig" &&
            result.Severity == DoctorCheckSeverity.Warning);
    }

    [Fact]
    public async Task RunAsync_MalformedRuntimeConfigProducesError()
    {
        using var temp = DoctorTestWorkspace.Create();
        File.WriteAllText(temp.RuntimeConfigPath, "{ invalid");
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "host.runtimeconfig" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_IncompatibleRuntimeMajorProducesWarning()
    {
        using var temp = DoctorTestWorkspace.Create(runtimeVersion: "99.0.0");
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "host.runtimeconfig" &&
            result.Severity == DoctorCheckSeverity.Warning &&
            result.Message.Contains(".NET 99", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_SelfContainedRuntimeConfigProducesOk()
    {
        using var temp = DoctorTestWorkspace.Create();
        temp.WriteSelfContainedRuntimeConfig("8.0.26");
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "host.runtimeconfig" &&
            result.Severity == DoctorCheckSeverity.Ok &&
            result.Message.Contains("self-contained deployment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_MissingWorkingDirectoryProducesError()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile(workingDirectory: Path.Combine(temp.DirectoryPath, "missing-workdir"));
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "profile.validation.host_working_directory_not_found" &&
            result.Severity == DoctorCheckSeverity.Error);
        Assert.Contains(report.Results, result =>
            result.Id == "host.workingDirectory" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_MissingLogFileIsNotAnError()
    {
        using var temp = DoctorTestWorkspace.Create();
        var missingLogPath = Path.Combine(temp.DirectoryPath, "missing-log-dir", "runtime.log");
        var profilePath = temp.WriteProfile(logPath: missingLogPath);
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "logs.candidate.ProfileConfigured" &&
            result.Severity == DoctorCheckSeverity.Ok &&
            result.Message.Contains("does not exist yet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_UnwritableLogDirectoryIsReported()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile(logPath: Path.Combine(temp.DirectoryPath, "runtime.log"));
        var writableProbe = new ThrowingWritableDirectoryProbe();
        var runner = new DoctorRunner(
            excelProbe: RecordingExcelProbe.Ok(),
            writableDirectoryProbe: writableProbe);

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "doctor.checkFailed" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_UnsupportedStdoutPolicyProducesError()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile(stdoutPolicy: "diagnosticsAllowed");
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Ok());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "profile.validation.unsupported_stdout_policy" &&
            result.Severity == DoctorCheckSeverity.Error);
        Assert.Contains(report.Results, result =>
            result.Id == "logs.stdoutPolicy" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_ExcelProbeCanReturnNonWindowsWarning()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: RecordingExcelProbe.Warning("Excel availability checks require Windows."));

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "excel.availability" &&
            result.Severity == DoctorCheckSeverity.Warning &&
            result.Message.Contains("Windows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_DefaultExcelProbeModeIsPassive()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile();
        var excelProbe = RecordingExcelProbe.Ok();
        var runner = new DoctorRunner(excelProbe: excelProbe);

        _ = await runner.RunAsync(profilePath);

        var request = Assert.Single(excelProbe.Requests);
        Assert.Equal(ExcelAvailabilityProbeMode.Passive, request.Mode);
    }

    [Fact]
    public async Task RunAsync_ActiveExcelProbeRequiresOption()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile();
        var excelProbe = RecordingExcelProbe.Ok();
        var runner = new DoctorRunner(excelProbe: excelProbe);

        _ = await runner.RunAsync(profilePath, new DoctorOptions { AllowActiveExcelComProbe = true });

        var request = Assert.Single(excelProbe.Requests);
        Assert.Equal(ExcelAvailabilityProbeMode.Active, request.Mode);
    }

    [Fact]
    public async Task RunAsync_ProbeFailureDoesNotThrow()
    {
        using var temp = DoctorTestWorkspace.Create();
        var profilePath = temp.WriteProfile();
        var runner = new DoctorRunner(excelProbe: new ThrowingExcelProbe());

        var report = await runner.RunAsync(profilePath);

        Assert.Contains(report.Results, result =>
            result.Id == "excel.availability" &&
            result.Severity == DoctorCheckSeverity.Error);
    }

    private sealed class RecordingExcelProbe : IExcelAvailabilityProbe
    {
        private readonly ExcelAvailabilityProbeResult _result;

        private RecordingExcelProbe(ExcelAvailabilityProbeResult result)
        {
            _result = result;
        }

        public List<ExcelAvailabilityProbeRequest> Requests { get; } = [];

        public static RecordingExcelProbe Ok() =>
            new(new ExcelAvailabilityProbeResult(DoctorCheckSeverity.Ok, "Excel probe ok.", "No action needed."));

        public static RecordingExcelProbe Warning(string message) =>
            new(new ExcelAvailabilityProbeResult(DoctorCheckSeverity.Warning, message, "Check Excel installation."));

        public Task<ExcelAvailabilityProbeResult> CheckAsync(
            ExcelAvailabilityProbeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingExcelProbe : IExcelAvailabilityProbe
    {
        public Task<ExcelAvailabilityProbeResult> CheckAsync(
            ExcelAvailabilityProbeRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("probe failed");
    }

    private sealed class ThrowingWritableDirectoryProbe : IWritableDirectoryProbe
    {
        public DoctorCheckResult CheckWritable(string directoryPath, string checkId, string checkName) =>
            throw new IOException("not writable");
    }

    private sealed class DoctorTestWorkspace : IDisposable
    {
        private DoctorTestWorkspace(string directoryPath, string commandPath)
        {
            DirectoryPath = directoryPath;
            CommandPath = commandPath;
            RuntimeConfigPath = Path.Combine(
                directoryPath,
                Path.GetFileNameWithoutExtension(commandPath) + ".runtimeconfig.json");
        }

        public string DirectoryPath { get; }

        public string CommandPath { get; }

        public string RuntimeConfigPath { get; }

        public static DoctorTestWorkspace Create(
            bool writeRuntimeConfig = true,
            string runtimeVersion = "8.0.0")
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var commandPath = Path.Combine(directoryPath, "GridPilotHost.exe");
            File.WriteAllText(commandPath, string.Empty);

            var workspace = new DoctorTestWorkspace(directoryPath, commandPath);
            if (writeRuntimeConfig)
            {
                workspace.WriteRuntimeConfig(runtimeVersion);
            }

            return workspace;
        }

        public string WriteProfile(
            string? commandPath = null,
            string? workingDirectory = null,
            string? logPath = null,
            string stdoutPolicy = "jsonRpcOnly")
        {
            var profilePath = Path.Combine(DirectoryPath, "profile.json");
            var json = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    name = "gridpilot-default",
                    displayName = "GridPilot MCP",
                    host = new
                    {
                        command = commandPath ?? CommandPath,
                        args = new[] { "--session-mode", "attach", "--attach-target", "workbook-owner" },
                        workingDirectory = workingDirectory ?? DirectoryPath,
                        env = new Dictionary<string, string?>
                        {
                            ["GRIDPILOT_LOG_LEVEL"] = "info"
                        }
                    },
                    logs = new
                    {
                        path = logPath,
                        stdoutPolicy
                    },
                    metadata = new
                    {
                        description = "Test profile"
                    }
                },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(profilePath, json);
            return profilePath;
        }

        public void WriteRuntimeConfig(string runtimeVersion)
        {
            var json = JsonSerializer.Serialize(
                new
                {
                    runtimeOptions = new
                    {
                        tfm = "net8.0",
                        framework = new
                        {
                            name = "Microsoft.NETCore.App",
                            version = runtimeVersion
                        }
                    }
                },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(RuntimeConfigPath, json);
        }

        public void WriteSelfContainedRuntimeConfig(string runtimeVersion)
        {
            var json = JsonSerializer.Serialize(
                new
                {
                    runtimeOptions = new
                    {
                        tfm = "net8.0",
                        includedFrameworks = new[]
                        {
                            new
                            {
                                name = "Microsoft.NETCore.App",
                                version = runtimeVersion
                            }
                        }
                    }
                },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(RuntimeConfigPath, json);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
