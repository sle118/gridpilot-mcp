namespace ExcelMcp.Core;

public sealed record SheetSummary(string Name, string Kind, bool Visible, string Visibility, int Index)
{
    public SheetSummary(string Name, string Kind, bool Visible)
        : this(Name, Kind, Visible, Visible ? "visible" : "hidden", 0)
    {
    }
}
