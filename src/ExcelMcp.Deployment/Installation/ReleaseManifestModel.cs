using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Installation;

internal sealed record ReleaseManifestModel
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}
