namespace ExcelMcp.Deployment.SmokeTests;

public sealed record McpSmokeTestOptions
{
    public static IReadOnlyList<string> DefaultExpectedToolNames { get; } =
    [
        "session_list_open_workbooks",
        "session_connect_workbook",
        "workbook_list_inventory",
        "range_read",
        "range_write",
        "calculation_inspect_errors"
    ];

    public McpSmokeTestTransportMode RequestTransportMode { get; init; } = McpSmokeTestTransportMode.Framed;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public IReadOnlyList<string> ExpectedToolNames { get; init; } = DefaultExpectedToolNames;

    public int StderrTailMaxChars { get; init; } = 4096;

    public string ProtocolVersion { get; init; } = "2024-11-05";
}
