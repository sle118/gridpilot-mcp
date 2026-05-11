using System.Text.Json;

namespace ExcelMcp.Deployment.Profiles;

public static class LaunchProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static LaunchProfileLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure("profile_path_required", "Launch profile path is required.", "$");
        }

        if (!File.Exists(path))
        {
            return Failure("profile_not_found", $"Launch profile file '{path}' does not exist.", "$");
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = ParseJson(json);
            var profile = DeserializeProfile(document);
            if (profile is null)
            {
                return Failure("profile_deserialization_failed", "Launch profile JSON did not produce a profile.", "$");
            }

            return new LaunchProfileLoadResult(profile, Array.Empty<LaunchProfileIssue>());
        }
        catch (JsonException ex)
        {
            var code = ex.Data.Contains("gridpilot.profile.parse")
                ? "profile_invalid_json"
                : "profile_deserialization_failed";
            return Failure(code, $"Launch profile JSON is invalid: {ex.Message}", ex.Path);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Failure("profile_unreadable", $"Launch profile file '{path}' could not be read: {ex.Message}", "$");
        }
        catch (IOException ex)
        {
            return Failure("profile_unreadable", $"Launch profile file '{path}' could not be read: {ex.Message}", "$");
        }
    }

    private static JsonDocument ParseJson(string json)
    {
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            ex.Data["gridpilot.profile.parse"] = true;
            throw;
        }
    }

    private static LaunchProfile? DeserializeProfile(JsonDocument document) =>
        document.RootElement.Deserialize<LaunchProfile>(JsonOptions);

    private static LaunchProfileLoadResult Failure(string code, string message, string? path) =>
        new(
            null,
            [
                new LaunchProfileIssue(
                    LaunchProfileIssueSeverity.Error,
                    code,
                    message,
                    path)
            ]);
}
