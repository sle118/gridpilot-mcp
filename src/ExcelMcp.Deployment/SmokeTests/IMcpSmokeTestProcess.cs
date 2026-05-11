namespace ExcelMcp.Deployment.SmokeTests;

public interface IMcpSmokeTestProcess : IAsyncDisposable
{
    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    Stream StandardError { get; }

    bool HasExited { get; }

    int? ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken = default);

    void Kill();
}
