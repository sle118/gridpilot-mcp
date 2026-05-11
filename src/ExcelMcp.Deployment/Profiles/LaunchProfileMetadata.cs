using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileMetadata
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

