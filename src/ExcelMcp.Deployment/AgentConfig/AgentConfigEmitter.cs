using System.Text;
using System.Text.Json;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.AgentConfig;

public static class AgentConfigEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AgentConfigSnippet Emit(LaunchProfile profile, AgentTarget target)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var descriptor = GetDescriptor(target);
        var validation = LaunchProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            return new AgentConfigSnippet(
                target,
                descriptor.DisplayName,
                descriptor.SuggestedFileName,
                descriptor.Language,
                string.Empty,
                validation.Issues
                    .Select(issue => new AgentConfigIssue(
                        issue.Severity == LaunchProfileIssueSeverity.Warning
                            ? AgentConfigIssueSeverity.Warning
                            : AgentConfigIssueSeverity.Error,
                        issue.Code,
                        issue.Message))
                    .ToArray());
        }

        var issues = new List<AgentConfigIssue>();
        var content = target switch
        {
            AgentTarget.VsCodeCopilot => EmitVsCodeCopilot(profile, issues),
            AgentTarget.CodexCli => EmitCodexCli(profile),
            AgentTarget.ClaudeCode => EmitClaudeCode(profile, issues),
            AgentTarget.GenericMcpJson => EmitGenericMcpJson(profile),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported agent target.")
        };

        return new AgentConfigSnippet(
            target,
            descriptor.DisplayName,
            descriptor.SuggestedFileName,
            descriptor.Language,
            content,
            issues);
    }

    private static string EmitVsCodeCopilot(LaunchProfile profile, List<AgentConfigIssue> issues)
    {
        WarnForUnsupportedWorkingDirectory(profile, issues, "vscode_cwd_not_emitted", "VS Code / GitHub Copilot MCP stdio configuration does not document a working directory field, so host.workingDirectory was not emitted.");

        var root = new Dictionary<string, object?>
        {
            ["servers"] = new Dictionary<string, object?>
            {
                [profile.Name!] = BuildJsonServer(profile, includeCwd: false)
            }
        };

        return SerializeJson(root);
    }

    private static string EmitClaudeCode(LaunchProfile profile, List<AgentConfigIssue> issues)
    {
        WarnForUnsupportedWorkingDirectory(profile, issues, "claude_cwd_not_emitted", "Claude Code project MCP configuration examples do not document a working directory field for stdio entries, so host.workingDirectory was not emitted.");

        var root = new Dictionary<string, object?>
        {
            ["mcpServers"] = new Dictionary<string, object?>
            {
                [profile.Name!] = BuildJsonServer(profile, includeCwd: false)
            }
        };

        return SerializeJson(root);
    }

    private static string EmitGenericMcpJson(LaunchProfile profile)
    {
        var root = new Dictionary<string, object?>
        {
            ["mcpServers"] = new Dictionary<string, object?>
            {
                [profile.Name!] = BuildJsonServer(profile, includeCwd: true)
            }
        };

        return SerializeJson(root);
    }

    private static string EmitCodexCli(LaunchProfile profile)
    {
        var host = profile.Host!;
        var builder = new StringBuilder();
        var tableKey = EscapeTomlKey(profile.Name!);

        builder.Append("[mcp_servers.")
            .Append(tableKey)
            .Append("]\n");
        builder.Append("command = ")
            .Append(ToTomlString(host.Command!))
            .Append('\n');
        builder.Append("args = ")
            .Append(ToTomlArray(host.Args ?? Array.Empty<string>()))
            .Append('\n');
        if (!string.IsNullOrWhiteSpace(host.WorkingDirectory))
        {
            builder.Append("cwd = ")
                .Append(ToTomlString(host.WorkingDirectory))
                .Append('\n');
        }

        builder.Append("enabled = true\n");
        builder.Append('\n');
        builder.Append("[mcp_servers.")
            .Append(tableKey)
            .Append(".env]\n");
        foreach (var (key, value) in GetOrderedEnv(profile))
        {
            builder.Append(EscapeTomlKey(key))
                .Append(" = ")
                .Append(ToTomlString(value))
                .Append('\n');
        }

        return builder.ToString();
    }

    private static Dictionary<string, object?> BuildJsonServer(LaunchProfile profile, bool includeCwd)
    {
        var host = profile.Host!;
        var server = new Dictionary<string, object?>
        {
            ["type"] = "stdio",
            ["command"] = host.Command,
            ["args"] = host.Args ?? Array.Empty<string>(),
            ["env"] = GetOrderedEnv(profile)
        };

        if (includeCwd && !string.IsNullOrWhiteSpace(host.WorkingDirectory))
        {
            server["cwd"] = host.WorkingDirectory;
        }

        return server;
    }

    private static SortedDictionary<string, string> GetOrderedEnv(LaunchProfile profile)
    {
        var env = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (profile.Host?.Env is null)
        {
            return env;
        }

        foreach (var (key, value) in profile.Host.Env)
        {
            if (value is not null)
            {
                env[key] = value;
            }
        }

        return env;
    }

    private static string SerializeJson(object value) =>
        JsonSerializer.Serialize(value, JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void WarnForUnsupportedWorkingDirectory(
        LaunchProfile profile,
        List<AgentConfigIssue> issues,
        string code,
        string message)
    {
        if (!string.IsNullOrWhiteSpace(profile.Host?.WorkingDirectory))
        {
            issues.Add(new AgentConfigIssue(AgentConfigIssueSeverity.Warning, code, message));
        }
    }

    private static (string DisplayName, string SuggestedFileName, string Language) GetDescriptor(AgentTarget target) =>
        target switch
        {
            AgentTarget.VsCodeCopilot => ("VS Code / GitHub Copilot", "mcp.json", "json"),
            AgentTarget.CodexCli => ("Codex CLI", "config.toml", "toml"),
            AgentTarget.ClaudeCode => ("Claude Code", ".mcp.json", "json"),
            AgentTarget.GenericMcpJson => ("Generic MCP JSON", "mcp.json", "json"),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported agent target.")
        };

    private static string ToTomlArray(IEnumerable<string> values) =>
        "[" + string.Join(", ", values.Select(ToTomlString)) + "]";

    private static string ToTomlString(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";

    private static string EscapeTomlKey(string key)
    {
        if (key.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-'))
        {
            return key;
        }

        return ToTomlString(key);
    }
}
