using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Bridge.Services;
using ExcelMcp.Core;
using System.Text.Json;
using ExcelMcp.ToolHost;

namespace ExcelMcp.ToolHost.Mcp;

public sealed class McpToolServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWorkbookServiceResolver _workbookServices;

    internal McpToolServer(IWorkbookServiceResolver workbookServices)
    {
        _workbookServices = workbookServices;
    }

    public McpToolServer(WorkbookService workbookService)
        : this(new SharedWorkbookServiceResolver(workbookService))
    {
    }

    public McpInitializeResult Initialize(string? requestedProtocolVersion)
    {
        var protocolVersion = string.IsNullOrWhiteSpace(requestedProtocolVersion)
            ? "2024-11-05"
            : requestedProtocolVersion;

        return new McpInitializeResult(
            protocolVersion,
            Capabilities: new { tools = new { listChanged = false } },
            ServerInfo: new { name = "GridPilot MCP", version = "0.1.0" });
    }

    public IReadOnlyList<McpToolDefinition> ListTools() =>
    [
        new(
            ToolNames.WorkbookListInventory,
            "List workbook sheets, tables, connections, and queries.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" }
                },
                required = new[] { "workbookPath" }
            })),
        new(
            ToolNames.QueryGet,
            "Get a workbook query definition by name.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    queryName = new { type = "string" }
                },
                required = new[] { "workbookPath", "queryName" }
            })),
        new(
            ToolNames.QueryRefresh,
            "Run a targeted refresh for one workbook query.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    queryName = new { type = "string" },
                    silent = new { type = "boolean" },
                    preferSynchronousTableRefresh = new { type = "boolean" },
                    timeoutMs = new { type = "integer" }
                },
                required = new[] { "workbookPath", "queryName" }
            })),
        new(
            ToolNames.QueryRunProbe,
            "Create a temporary diagnostic query, load preview rows, and clean up probe artifacts.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    queryName = new { type = "string" },
                    tempPrefix = new { type = "string" }
                },
                required = new[] { "workbookPath", "queryName" }
            })),
        new(
            ToolNames.QueryCleanupTemp,
            "Delete temporary queries matching a prefix or pattern.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    pattern = new { type = "string" }
                },
                required = new[] { "workbookPath", "pattern" }
            }))
    ];

    public async Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            object structuredContent = name switch
            {
                ToolNames.WorkbookListInventory => await HandleListInventoryAsync(arguments, cancellationToken),
                ToolNames.QueryGet => await HandleGetQueryAsync(arguments, cancellationToken),
                ToolNames.QueryRefresh => await HandleRefreshAsync(arguments, cancellationToken),
                ToolNames.QueryRunProbe => await HandleProbeAsync(arguments, cancellationToken),
                ToolNames.QueryCleanupTemp => await HandleCleanupAsync(arguments, cancellationToken),
                _ => throw new McpToolInputException("invalid_tool", $"Unknown tool '{name}'.")
            };

            var structuredJson = ToJsonElement(structuredContent);
            return new McpToolCallResult(
                Content: new object[] { new { type = "text", text = JsonSerializer.Serialize(structuredContent, JsonOptions) } },
                StructuredContent: structuredJson,
                IsError: IsErrorResult(structuredJson));
        }
        catch (McpToolInputException ex)
        {
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, Source: nameof(McpToolServer)));
        }
        catch (ExcelSessionTargetException ex)
        {
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, ex.Detail, nameof(McpToolServer)));
        }
        catch (Exception ex)
        {
            return BuildErrorResult(new McpToolError("tool_call_failed", ex.Message, ex.InnerException?.Message, nameof(McpToolServer)));
        }
    }

    private async Task<object> HandleListInventoryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.ListInventoryAsync(workbookPath, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleGetQueryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var queryName = GetRequiredString(arguments, "queryName");
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.GetQueryAsync(workbookPath, queryName, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleRefreshAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var queryName = GetRequiredString(arguments, "queryName");
        var options = new RefreshOptions(
            Silent: GetOptionalBoolean(arguments, "silent") ?? true,
            PreferSynchronousTableRefresh: GetOptionalBoolean(arguments, "preferSynchronousTableRefresh") ?? true,
            Timeout: GetOptionalInt32(arguments, "timeoutMs") is int timeoutMs ? TimeSpan.FromMilliseconds(timeoutMs) : null);

        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.RefreshQueryAsync(workbookPath, queryName, options, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleProbeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var queryName = GetRequiredString(arguments, "queryName");
        var tempPrefix = GetOptionalString(arguments, "tempPrefix") ?? "tmp_probe_mcp";
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.TryRunQueryAsync(workbookPath, queryName, tempPrefix, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleCleanupAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var pattern = GetRequiredString(arguments, "pattern");
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.CleanupTempQueriesAsync(workbookPath, pattern, cancellationToken),
            cancellationToken);
    }

    private static bool IsErrorResult(JsonElement result) =>
        result.ValueKind == JsonValueKind.Object &&
        result.TryGetProperty("succeeded", out var succeeded) &&
        succeeded.ValueKind == JsonValueKind.False;

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            property.GetString() is { Length: > 0 } value)
        {
            return value;
        }

        throw new McpToolInputException("invalid_arguments", $"Missing required string argument '{propertyName}'.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static bool? GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return null;
    }

    private static int? GetOptionalInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private static JsonElement ToJsonElement(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions);

    private static McpToolCallResult BuildErrorResult(McpToolError error)
    {
        var payload = new { error };
        var structuredJson = ToJsonElement(payload);
        return new McpToolCallResult(
            Content: new object[] { new { type = "text", text = JsonSerializer.Serialize(payload, JsonOptions) } },
            StructuredContent: structuredJson,
            IsError: true);
    }

    private sealed class SharedWorkbookServiceResolver : IWorkbookServiceResolver
    {
        private readonly WorkbookService _workbookService;

        public SharedWorkbookServiceResolver(WorkbookService workbookService)
        {
            _workbookService = workbookService;
        }

        public Task<T> ExecuteAsync<T>(string workbookPath, Func<WorkbookService, Task<T>> action, CancellationToken cancellationToken = default) =>
            action(_workbookService);
    }
}
