using System.Text.Json.Serialization;

namespace ExcelMcp.Deployment.Installation;

internal sealed record InstallationMetadata
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("scope")]
    public InstallScope Scope { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("installRoot")]
    public string? InstallRoot { get; init; }

    [JsonPropertyName("profileRoot")]
    public string? ProfileRoot { get; init; }

    [JsonPropertyName("logRoot")]
    public string? LogRoot { get; init; }

    [JsonPropertyName("startMenuProgramsRoot")]
    public string? StartMenuProgramsRoot { get; init; }

    [JsonPropertyName("trayExecutablePath")]
    public string? TrayExecutablePath { get; init; }

    [JsonPropertyName("setupExecutablePath")]
    public string? SetupExecutablePath { get; init; }

    [JsonPropertyName("hostExecutablePath")]
    public string? HostExecutablePath { get; init; }

    [JsonPropertyName("proxyExecutablePath")]
    public string? ProxyExecutablePath { get; init; }

    [JsonPropertyName("defaultProfilePath")]
    public string? DefaultProfilePath { get; init; }

    [JsonPropertyName("metadataPath")]
    public string? MetadataPath { get; init; }

    [JsonPropertyName("installedAtUtc")]
    public DateTimeOffset? InstalledAtUtc { get; init; }
}
