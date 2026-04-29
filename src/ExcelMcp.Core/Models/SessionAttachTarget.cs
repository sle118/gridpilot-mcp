namespace ExcelMcp.Core;

public enum SessionAttachTargetMode
{
    AnyRunningInstance = 0,
    WorkbookOwner = 1
}

public sealed record SessionAttachTarget(SessionAttachTargetMode Mode, string? WorkbookPath = null)
{
    public static SessionAttachTarget AnyRunningInstance { get; } = new(SessionAttachTargetMode.AnyRunningInstance);

    public static SessionAttachTarget ForWorkbook(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        return new SessionAttachTarget(SessionAttachTargetMode.WorkbookOwner, workbookPath);
    }
}
