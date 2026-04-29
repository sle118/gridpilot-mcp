namespace ExcelMcp.Core;

public sealed class ExcelSessionTargetException : InvalidOperationException
{
    public ExcelSessionTargetException(string code, string message, string? detail = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Detail = detail;
    }

    public string Code { get; }

    public string? Detail { get; }
}
