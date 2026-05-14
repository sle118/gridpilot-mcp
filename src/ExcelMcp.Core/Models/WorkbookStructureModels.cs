namespace ExcelMcp.Core;

public static class WorkbookVisibilityModes
{
    public const string Visible = "visible";
    public const string Hidden = "hidden";
}

public static class WorkbookProtectionModes
{
    public const string Protect = "protect";
    public const string Unprotect = "unprotect";
}

public sealed record WorkbookVisibilityRequest(string Visibility);

public sealed record WorkbookProtectionUpdateRequest(
    string Mode,
    string? Password = null,
    bool? ProtectStructure = null,
    bool? ProtectWindows = null);

public sealed record WorkbookProtectionState(
    bool IsProtected,
    bool ProtectStructure,
    bool ProtectWindows);

public sealed record WorkbookStructureState(
    string Visibility,
    WorkbookProtectionState Protection);
