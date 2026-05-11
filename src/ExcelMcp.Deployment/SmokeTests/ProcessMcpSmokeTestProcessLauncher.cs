using System.Diagnostics;

namespace ExcelMcp.Deployment.SmokeTests;

public sealed class ProcessMcpSmokeTestProcessLauncher : IMcpSmokeTestProcessLauncher
{
    public IMcpSmokeTestProcess Launch(McpSmokeTestProcessStartInfo startInfo)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = startInfo.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in startInfo.Args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
        {
            processStartInfo.WorkingDirectory = startInfo.WorkingDirectory;
        }

        foreach (var (key, value) in startInfo.Environment)
        {
            processStartInfo.Environment[key] = value;
        }

        var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start MCP smoke-test process.");
        return new ProcessMcpSmokeTestProcess(process);
    }

    private sealed class ProcessMcpSmokeTestProcess : IMcpSmokeTestProcess
    {
        private readonly Process _process;

        public ProcessMcpSmokeTestProcess(Process process)
        {
            _process = process;
        }

        public Stream StandardInput => _process.StandardInput.BaseStream;

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

        public Stream StandardError => _process.StandardError.BaseStream;

        public bool HasExited => _process.HasExited;

        public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

        public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
            _process.WaitForExitAsync(cancellationToken);

        public void Kill()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }

        public ValueTask DisposeAsync()
        {
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
