namespace ExcelMcp.Core;

public sealed record RangeFormatSnapshot(
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
    double? ColumnWidth = null);
