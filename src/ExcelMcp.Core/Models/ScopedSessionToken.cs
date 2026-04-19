namespace ExcelMcp.Core;

public sealed record ScopedSessionToken(Guid Value)
{
    public static ScopedSessionToken New() => new(Guid.NewGuid());
}
