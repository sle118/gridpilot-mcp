namespace ExcelMcp.Core;

public sealed record RangeFormatPatch(
    string? NumberFormat = null,
    string? FontName = null,
    double? FontSize = null,
    bool? Bold = null,
    bool? Italic = null,
    string? FontColor = null,
    bool? HasFill = null,
    string? FillColor = null,
    string? HorizontalAlignment = null,
    string? VerticalAlignment = null,
    bool? WrapText = null,
    double? RowHeight = null,
    double? ColumnWidth = null)
{
    public bool IsEmpty =>
        NumberFormat is null &&
        FontName is null &&
        FontSize is null &&
        Bold is null &&
        Italic is null &&
        FontColor is null &&
        HasFill is null &&
        FillColor is null &&
        HorizontalAlignment is null &&
        VerticalAlignment is null &&
        WrapText is null &&
        RowHeight is null &&
        ColumnWidth is null;
}
