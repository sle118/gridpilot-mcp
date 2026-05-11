using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileLogs
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("stdoutPolicy")]
    public string? StdoutPolicy { get; init; }
}

