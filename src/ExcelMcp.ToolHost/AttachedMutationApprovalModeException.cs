namespace ExcelMcp.ToolHost;

internal sealed class AttachedMutationApprovalModeException : Exception
{
    public AttachedMutationApprovalModeException(string code, string message, string? detail = null)
        : base(message)
    {
        Code = code;
        Detail = detail;
    }

    public string Code { get; }

    public string? Detail { get; }
}
