namespace ExcelMcp.Deployment.Profiles;

public static class LaunchProfileValidator
{
    public const int SupportedSchemaVersion = 1;
    public const string JsonRpcOnlyStdoutPolicy = "jsonRpcOnly";

    public static LaunchProfileValidationResult Validate(LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var issues = new List<LaunchProfileIssue>();

        if (profile.SchemaVersion != SupportedSchemaVersion)
        {
            issues.Add(Error(
                "unsupported_schema_version",
                $"Launch profile schemaVersion must be {SupportedSchemaVersion}.",
                "$.schemaVersion"));
        }

        RequireNonEmpty(profile.Name, "name_required", "Launch profile name is required.", "$.name", issues);
        RequireNonEmpty(profile.DisplayName, "display_name_required", "Launch profile displayName is required.", "$.displayName", issues);

        ValidateHost(profile.Host, issues);
        ValidateLogs(profile.Logs, issues);

        return new LaunchProfileValidationResult(issues);
    }

    private static void ValidateHost(LaunchProfileHost? host, List<LaunchProfileIssue> issues)
    {
        if (host is null)
        {
            issues.Add(Error("host_required", "Launch profile host section is required.", "$.host"));
            return;
        }

        if (RequireNonEmpty(host.Command, "host_command_required", "Launch profile host.command is required.", "$.host.command", issues) &&
            !File.Exists(host.Command))
        {
            issues.Add(Error(
                "host_command_not_found",
                $"Launch profile host.command '{host.Command}' does not exist.",
                "$.host.command"));
        }

        if (host.Args is null)
        {
            issues.Add(Error("host_args_required", "Launch profile host.args must be an array.", "$.host.args"));
        }

        if (host.WorkingDirectory is { Length: > 0 } workingDirectory && !Directory.Exists(workingDirectory))
        {
            issues.Add(Error(
                "host_working_directory_not_found",
                $"Launch profile host.workingDirectory '{workingDirectory}' does not exist.",
                "$.host.workingDirectory"));
        }

        if (host.Env is null)
        {
            issues.Add(Error("host_env_required", "Launch profile host.env must be an object.", "$.host.env"));
            return;
        }

        foreach (var (key, value) in host.Env)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add(Error("host_env_key_required", "Launch profile host.env contains an empty environment variable name.", "$.host.env"));
            }

            if (value is null)
            {
                issues.Add(Error(
                    "host_env_value_required",
                    $"Launch profile host.env['{key}'] must not be null.",
                    $"$.host.env.{key}"));
            }
        }
    }

    private static void ValidateLogs(LaunchProfileLogs? logs, List<LaunchProfileIssue> issues)
    {
        if (logs is null)
        {
            issues.Add(Error("logs_required", "Launch profile logs section is required.", "$.logs"));
            return;
        }

        if (!RequireNonEmpty(logs.StdoutPolicy, "logs_stdout_policy_required", "Launch profile logs.stdoutPolicy is required.", "$.logs.stdoutPolicy", issues))
        {
            return;
        }

        if (!string.Equals(logs.StdoutPolicy, JsonRpcOnlyStdoutPolicy, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "unsupported_stdout_policy",
                $"Launch profile logs.stdoutPolicy must be '{JsonRpcOnlyStdoutPolicy}'.",
                "$.logs.stdoutPolicy"));
        }
    }

    private static bool RequireNonEmpty(
        string? value,
        string code,
        string message,
        string path,
        List<LaunchProfileIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        issues.Add(Error(code, message, path));
        return false;
    }

    private static LaunchProfileIssue Error(string code, string message, string path) =>
        new(LaunchProfileIssueSeverity.Error, code, message, path);
}

