namespace ExcelMcp.Deployment.Logs;

public sealed record RecentLogReadOptions(int MaxLines = 100, int MaxBytes = 64 * 1024)
{
    public static RecentLogReadOptions Default { get; } = new();
}
