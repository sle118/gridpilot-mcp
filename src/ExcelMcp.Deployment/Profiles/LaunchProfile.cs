using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfile
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("host")]
    public LaunchProfileHost? Host { get; init; }

    [JsonPropertyName("logs")]
    public LaunchProfileLogs? Logs { get; init; }

    [JsonPropertyName("metadata")]
    public LaunchProfileMetadata? Metadata { get; init; }
}

