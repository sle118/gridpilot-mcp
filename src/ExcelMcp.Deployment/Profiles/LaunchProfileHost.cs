using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileHost
{
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("args")]
    public IReadOnlyList<string>? Args { get; init; }

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; init; }

    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string?>? Env { get; init; }
}

