using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Bridge.Services;
using ExcelMcp.Core;
using ExcelMcp.Core.Results;
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
            })),
        new(
            ToolNames.QuerySetFormula,
            "Set or replace a workbook query formula by name.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    queryName = new { type = "string" },
                    formula = new { type = "string" }
                },
                required = new[] { "workbookPath", "queryName", "formula" }
            })),
        new(
            ToolNames.RangeRead,
            "Read one rectangular workbook range from a specific worksheet.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    sheetName = new { type = "string" },
                    address = new { type = "string" }
                },
                required = new[] { "workbookPath", "sheetName", "address" }
            })),
        new(
            ToolNames.RangeWrite,
            "Write one or more rectangular workbook ranges.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    writes = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                sheetName = new { type = "string" },
                                address = new { type = "string" },
                                values = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "array"
                                    }
                                }
                            },
                            required = new[] { "sheetName", "address", "values" }
                        }
                    }
                },
                required = new[] { "workbookPath", "writes" }
            })),
        new(
            ToolNames.AttachedSessionGrantMutation,
            "Grant a workbook-scoped attached-session mutation approval lease.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    ttlMinutes = new { type = "integer" }
                },
                required = new[] { "workbookPath" }
            })),
        new(
            ToolNames.AttachedSessionRevokeMutation,
            "Revoke a workbook-scoped attached-session mutation approval lease.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" }
                },
                required = new[] { "workbookPath" }
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
                ToolNames.QuerySetFormula => await HandleSetQueryFormulaAsync(arguments, cancellationToken),
                ToolNames.RangeRead => await HandleRangeReadAsync(arguments, cancellationToken),
                ToolNames.RangeWrite => await HandleRangeWriteAsync(arguments, cancellationToken),
                ToolNames.AttachedSessionGrantMutation => await HandleGrantApprovalAsync(arguments, cancellationToken),
                ToolNames.AttachedSessionRevokeMutation => await HandleRevokeApprovalAsync(arguments, cancellationToken),
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
        catch (AttachedMutationApprovalModeException ex)
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

    private async Task<object> HandleSetQueryFormulaAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var queryName = GetRequiredString(arguments, "queryName");
        var formula = GetRequiredString(arguments, "formula");
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.SetQueryFormulaAsync(workbookPath, queryName, formula, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleRangeReadAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var sheetName = GetRequiredString(arguments, "sheetName");
        var address = GetRequiredString(arguments, "address");
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.ReadRangeAsync(workbookPath, sheetName, address, cancellationToken),
            cancellationToken);
    }

    private async Task<object> HandleRangeWriteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var request = GetRangeWriteRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            workbookPath,
            service => service.WriteRangesAsync(workbookPath, request, cancellationToken),
            cancellationToken);
    }

    private Task<object> HandleGrantApprovalAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        var ttl = GetOptionalInt32(arguments, "ttlMinutes") is int ttlMinutes
            ? TimeSpan.FromMinutes(ttlMinutes)
            : (TimeSpan?)null;

        return ExecuteAsObjectAsync(_workbookServices.GrantAttachedMutationApprovalAsync(workbookPath, ttl, cancellationToken));
    }

    private Task<object> HandleRevokeApprovalAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var workbookPath = GetRequiredString(arguments, "workbookPath");
        return ExecuteAsObjectAsync(_workbookServices.RevokeAttachedMutationApprovalAsync(workbookPath, cancellationToken));
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

    private static RangeWriteRequest GetRangeWriteRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("writes", out var writesElement) ||
            writesElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "Missing required array argument 'writes'.");
        }

        var writes = new List<RangeWriteTarget>();
        foreach (var write in writesElement.EnumerateArray())
        {
            if (write.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each 'writes' item must be an object.");
            }

            var sheetName = GetRequiredString(write, "sheetName");
            var address = GetRequiredString(write, "address");
            if (!write.TryGetProperty("values", out var valuesElement))
            {
                throw new McpToolInputException("invalid_arguments", "Each range write must include 'values'.");
            }

            writes.Add(new RangeWriteTarget(sheetName, address, ParseMatrix(valuesElement)));
        }

        if (writes.Count == 0)
        {
            throw new McpToolInputException("invalid_arguments", "At least one range write target is required.");
        }

        return new RangeWriteRequest(writes);
    }

    private static object?[,] ParseMatrix(JsonElement valuesElement)
    {
        if (valuesElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "'values' must be a rectangular array of arrays.");
        }

        var rows = valuesElement.EnumerateArray().ToArray();
        if (rows.Length == 0)
        {
            throw new McpToolInputException("invalid_arguments", "'values' must contain at least one row.");
        }

        if (rows.Any(row => row.ValueKind != JsonValueKind.Array))
        {
            throw new McpToolInputException("invalid_arguments", "'values' must be a rectangular array of arrays.");
        }

        var columnCount = rows[0].GetArrayLength();
        if (columnCount == 0)
        {
            throw new McpToolInputException("invalid_arguments", "'values' rows must contain at least one column.");
        }

        if (rows.Any(row => row.GetArrayLength() != columnCount))
        {
            throw new McpToolInputException("invalid_arguments", "'values' must be rectangular.");
        }

        var matrix = new object?[rows.Length, columnCount];
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var cells = rows[rowIndex].EnumerateArray().ToArray();
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                matrix[rowIndex, columnIndex] = ParseCellValue(cells[columnIndex]);
            }
        }

        return matrix;
    }

    private static object? ParseCellValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number => element.GetDouble(),
            _ => throw new McpToolInputException("invalid_arguments", "Range write cell values must be scalars or null.")
        };

    private static async Task<object> ExecuteAsObjectAsync<T>(Task<T> task) where T : class =>
        await task.ConfigureAwait(false);

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

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(string workbookPath, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(new AttachedMutationApprovalModeException(
                "attached_session_approval_not_applicable",
                "Attached-session mutation approval is not available on a shared workbook service resolver.",
                "Use the host-owned workbook service resolver in attach mode."));

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(string workbookPath, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(new AttachedMutationApprovalModeException(
                "attached_session_approval_not_applicable",
                "Attached-session mutation approval is not available on a shared workbook service resolver.",
                "Use the host-owned workbook service resolver in attach mode."));
    }
}
