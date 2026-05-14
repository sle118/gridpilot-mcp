using System.Text.Json.Serialization;

namespace ExcelMcp.Core;

public sealed record SessionDiagnostics(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ExcelSessionMode>))] ExcelSessionMode SessionMode,
    bool IsReady,
    bool IsInteractive,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ExcelCalculationState>))] ExcelCalculationState CalculationState,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionAttachTargetMode>))] SessionAttachTargetMode? AttachTargetMode = null,
    bool IsEditingCell = false,
    bool HasModalUi = false,
    bool IsBusy = false);
