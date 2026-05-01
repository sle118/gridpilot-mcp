using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Bridge.Services;
using ExcelMcp.Core;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;
using System.Text.Json;
using ExcelMcp.ToolHost;

namespace ExcelMcp.ToolHost.Mcp;

public sealed class McpToolServer
{
    private static readonly TimeSpan DefaultToolExecutionTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IWorkbookServiceResolver _workbookServices;
    private readonly IGridPilotLogger _logger;
    private readonly TimeSpan _toolExecutionTimeout;

    internal McpToolServer(
        IWorkbookServiceResolver workbookServices,
        IGridPilotLogger? logger = null,
        TimeSpan? toolExecutionTimeout = null)
    {
        _workbookServices = workbookServices;
        _logger = logger ?? GridPilotNullLogger.Instance;
        _toolExecutionTimeout = toolExecutionTimeout ?? DefaultToolExecutionTimeout;
    }

    public McpToolServer(WorkbookService workbookService, IGridPilotLogger? logger = null, TimeSpan? toolExecutionTimeout = null)
        : this(new SharedWorkbookServiceResolver(workbookService), logger, toolExecutionTimeout)
    {
    }

    public McpInitializeResult Initialize(string? requestedProtocolVersion)
    {
        var protocolVersion = string.IsNullOrWhiteSpace(requestedProtocolVersion)
            ? "2024-11-05"
            : requestedProtocolVersion;

        _logger.LogInfo(nameof(McpToolServer), "initialize", new Dictionary<string, object?>
        {
            ["requestedProtocolVersion"] = requestedProtocolVersion,
            ["resolvedProtocolVersion"] = protocolVersion
        });

        return new McpInitializeResult(
            protocolVersion,
            Capabilities: new { tools = new { listChanged = false } },
            ServerInfo: new { name = "GridPilot MCP", version = "0.1.0" });
    }

    public IReadOnlyList<McpToolDefinition> ListTools()
    {
        _logger.LogDebug(nameof(McpToolServer), "list_tools");
        return
    [
        new(
            ToolNames.SessionListOpenWorkbooks,
            "List open Excel workbooks available for attachment.",
            ToJsonElement(new { type = "object", properties = new { } })),
        new(
            ToolNames.SessionConnectWorkbook,
            "Connect a workbook by full path or by open workbook title.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    workbookPath = new { type = "string" },
                    workbookName = new { type = "string" }
                }
            })),
        new(
            ToolNames.SessionCreateWorkbook,
            "Create a new workbook at a full path and connect it through a bridge-owned session.",
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
            ToolNames.SessionListConnections,
            "List connected workbooks tracked by this MCP host.",
            ToJsonElement(new { type = "object", properties = new { } })),
        new(
            ToolNames.SessionGetConnection,
            "Get one connected workbook by connection id.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    connectionId = new { type = "string" }
                },
                required = new[] { "connectionId" }
            })),
        new(
            ToolNames.SessionDisconnectWorkbook,
            "Disconnect one connected workbook by connection id.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    connectionId = new { type = "string" }
                },
                required = new[] { "connectionId" }
            })),
        new(
            ToolNames.WorkbookListInventory,
            "List workbook sheets, tables, connections, and queries.",
            BuildTargetSchema()),
        new(
            ToolNames.WorkbookListNames,
            "List workbook and worksheet-scoped Excel names.",
            BuildTargetSchema()),
        new(
            ToolNames.QueryGet,
            "Get a workbook query definition by name.",
            BuildTargetSchema(["queryName"], ("queryName", new { type = "string" }))),
        new(
            ToolNames.NameGet,
            "Resolve one workbook or worksheet-scoped Excel name.",
            BuildTargetSchema(
                ["name"],
                ("name", new { type = "string" }),
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.NameRead,
            "Read the values currently referenced by one Excel name.",
            BuildTargetSchema(
                ["name"],
                ("name", new { type = "string" }),
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.NameCreate,
            "Create a workbook or worksheet-scoped Excel name.",
            BuildTargetSchema(
                ["name", "refersTo"],
                ("name", new { type = "string" }),
                ("refersTo", new { type = "string" }),
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.NameUpdate,
            "Update the target formula for a workbook or worksheet-scoped Excel name.",
            BuildTargetSchema(
                ["name", "refersTo"],
                ("name", new { type = "string" }),
                ("refersTo", new { type = "string" }),
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.NameDelete,
            "Delete a workbook or worksheet-scoped Excel name.",
            BuildTargetSchema(
                ["name"],
                ("name", new { type = "string" }),
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.QueryRefresh,
            "Run a targeted refresh for one workbook query.",
            BuildTargetSchema(
                ["queryName"],
                ("queryName", new { type = "string" }),
                ("silent", new { type = "boolean" }),
                ("preferSynchronousTableRefresh", new { type = "boolean" }),
                ("timeoutMs", new { type = "integer" }))),
        new(
            ToolNames.QueryRunProbe,
            "Create a temporary diagnostic query, load preview rows, and clean up probe artifacts.",
            BuildTargetSchema(
                ["queryName"],
                ("queryName", new { type = "string" }),
                ("tempPrefix", new { type = "string" }))),
        new(
            ToolNames.QueryCleanupTemp,
            "Delete temporary queries matching a prefix or pattern.",
            BuildTargetSchema(
                ["pattern"],
                ("pattern", new { type = "string" }))),
        new(
            ToolNames.QuerySetFormula,
            "Set or replace a workbook query formula by name.",
            BuildTargetSchema(
                ["queryName", "formula"],
                ("queryName", new { type = "string" }),
                ("formula", new { type = "string" }))),
        new(
            ToolNames.TableGet,
            "Get deeper metadata for one Excel table.",
            BuildTargetSchema(
                ["tableName"],
                ("tableName", new { type = "string" }))),
        new(
            ToolNames.TableRead,
            "Read one Excel table with headers and rows.",
            BuildTargetSchema(
                ["tableName"],
                ("tableName", new { type = "string" }))),
        new(
            ToolNames.TableCreate,
            "Create one Excel table from an existing rectangular range.",
            BuildTargetSchema(
                ["tableName", "sheetName", "address"],
                ("tableName", new { type = "string" }),
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }),
                ("hasHeaders", new { type = "boolean" }))),
        new(
            ToolNames.TableResize,
            "Resize one existing Excel table to a new rectangular range.",
            BuildTargetSchema(
                ["tableName", "sheetName", "address"],
                ("tableName", new { type = "string" }),
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.TableAppendRows,
            "Append one or more rectangular data rows to an Excel table.",
            BuildTargetSchema(
                ["tableName", "values"],
                ("tableName", new { type = "string" }),
                ("values", new { type = "array", items = new { type = "array" } }))),
        new(
            ToolNames.TableReplaceRows,
            "Replace the data body rows for an Excel table.",
            BuildTargetSchema(
                ["tableName", "values"],
                ("tableName", new { type = "string" }),
                ("values", new { type = "array", items = new { type = "array" } }))),
        new(
            ToolNames.TableSetOptions,
            "Update supported table options such as headers and totals visibility.",
            BuildTargetSchema(
                ["tableName"],
                ("tableName", new { type = "string" }),
                ("hasHeaders", new { type = "boolean" }),
                ("showTotals", new { type = "boolean" }))),
        new(
            ToolNames.RangeRead,
            "Read one rectangular workbook range from a specific worksheet.",
            BuildTargetSchema(
                ["sheetName", "address"],
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.RangeWrite,
            "Write one or more rectangular workbook ranges.",
            BuildTargetSchema(
                ["writes"],
                ("writes", new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetName = new { type = "string" },
                            address = new { type = "string" },
                            values = new { type = "array", items = new { type = "array" } }
                        },
                        required = new[] { "sheetName", "address", "values" }
                    }
                }))),
        new(
            ToolNames.AttachedSessionGrantMutation,
            "Grant a workbook-scoped attached-session mutation approval lease.",
            BuildTargetSchema(
                ("ttlMinutes", new { type = "integer" }))),
        new(
            ToolNames.AttachedSessionRevokeMutation,
            "Revoke a workbook-scoped attached-session mutation approval lease.",
            BuildTargetSchema())
    ];
    }

    public async Task<McpToolCallResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_started", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["argumentKeys"] = GetArgumentKeys(arguments)
            });

            _logger.LogDebug(nameof(McpToolServer), "tool_call_dispatching", new Dictionary<string, object?>
            {
                ["toolName"] = name
            });

            var toolTask = Task.Run(
                async () => await DispatchToolAsync(name, arguments, cancellationToken).ConfigureAwait(false),
                cancellationToken);
            var timeoutTask = Task.Delay(_toolExecutionTimeout, cancellationToken);
            var completedTask = await Task.WhenAny(toolTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInfo(nameof(McpToolServer), "tool_call_timed_out", new Dictionary<string, object?>
                {
                    ["toolName"] = name,
                    ["timeoutMs"] = _toolExecutionTimeout.TotalMilliseconds
                });

                return BuildErrorResult(new McpToolError(
                    "tool_timeout",
                    $"Tool '{name}' timed out.",
                    $"Tool execution exceeded {_toolExecutionTimeout.TotalSeconds:0.###} seconds.",
                    nameof(McpToolServer)));
            }

            object structuredContent = await toolTask.ConfigureAwait(false);

            var structuredJson = ToJsonElement(structuredContent);
            _logger.LogInfo(nameof(McpToolServer), "tool_call_finished", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["isError"] = IsErrorResult(structuredJson)
            });
            return new McpToolCallResult(
                Content: new object[] { new { type = "text", text = JsonSerializer.Serialize(structuredContent, JsonOptions) } },
                StructuredContent: structuredJson,
                IsError: IsErrorResult(structuredJson));
        }
        catch (McpToolInputException ex)
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_failed", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["code"] = ex.Code
            }, ex);
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, Source: nameof(McpToolServer)));
        }
        catch (WorkbookTargetResolutionException ex)
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_failed", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["code"] = ex.Code
            }, ex);
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, ex.Detail, nameof(McpToolServer)));
        }
        catch (ExcelSessionTargetException ex)
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_failed", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["code"] = ex.Code
            }, ex);
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, ex.Detail, nameof(McpToolServer)));
        }
        catch (AttachedMutationApprovalModeException ex)
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_failed", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["code"] = ex.Code
            }, ex);
            return BuildErrorResult(new McpToolError(ex.Code, ex.Message, ex.Detail, nameof(McpToolServer)));
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(McpToolServer), "tool_call_failed", new Dictionary<string, object?>
            {
                ["toolName"] = name,
                ["code"] = "tool_call_failed"
            }, ex);
            return BuildErrorResult(new McpToolError("tool_call_failed", ex.Message, ex.InnerException?.Message, nameof(McpToolServer)));
        }
    }

    private Task<object> DispatchToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken) =>
        name switch
        {
            ToolNames.SessionListOpenWorkbooks => HandleListOpenWorkbooksAsync(cancellationToken),
            ToolNames.SessionConnectWorkbook => HandleConnectWorkbookAsync(arguments, cancellationToken),
            ToolNames.SessionCreateWorkbook => HandleCreateWorkbookAsync(arguments, cancellationToken),
            ToolNames.SessionListConnections => HandleListConnectionsAsync(cancellationToken),
            ToolNames.SessionGetConnection => HandleGetConnectionAsync(arguments, cancellationToken),
            ToolNames.SessionDisconnectWorkbook => HandleDisconnectWorkbookAsync(arguments, cancellationToken),
            ToolNames.WorkbookListInventory => HandleListInventoryAsync(arguments, cancellationToken),
            ToolNames.WorkbookListNames => HandleListNamesAsync(arguments, cancellationToken),
            ToolNames.QueryGet => HandleGetQueryAsync(arguments, cancellationToken),
            ToolNames.NameGet => HandleGetNameAsync(arguments, cancellationToken),
            ToolNames.NameRead => HandleReadNameAsync(arguments, cancellationToken),
            ToolNames.NameCreate => HandleCreateNameAsync(arguments, cancellationToken),
            ToolNames.NameUpdate => HandleUpdateNameAsync(arguments, cancellationToken),
            ToolNames.NameDelete => HandleDeleteNameAsync(arguments, cancellationToken),
            ToolNames.QueryRefresh => HandleRefreshAsync(arguments, cancellationToken),
            ToolNames.QueryRunProbe => HandleProbeAsync(arguments, cancellationToken),
            ToolNames.QueryCleanupTemp => HandleCleanupAsync(arguments, cancellationToken),
            ToolNames.QuerySetFormula => HandleSetQueryFormulaAsync(arguments, cancellationToken),
            ToolNames.TableGet => HandleTableGetAsync(arguments, cancellationToken),
            ToolNames.TableRead => HandleTableReadAsync(arguments, cancellationToken),
            ToolNames.TableCreate => HandleTableCreateAsync(arguments, cancellationToken),
            ToolNames.TableResize => HandleTableResizeAsync(arguments, cancellationToken),
            ToolNames.TableAppendRows => HandleTableAppendRowsAsync(arguments, cancellationToken),
            ToolNames.TableReplaceRows => HandleTableReplaceRowsAsync(arguments, cancellationToken),
            ToolNames.TableSetOptions => HandleTableSetOptionsAsync(arguments, cancellationToken),
            ToolNames.RangeRead => HandleRangeReadAsync(arguments, cancellationToken),
            ToolNames.RangeWrite => HandleRangeWriteAsync(arguments, cancellationToken),
            ToolNames.AttachedSessionGrantMutation => HandleGrantApprovalAsync(arguments, cancellationToken),
            ToolNames.AttachedSessionRevokeMutation => HandleRevokeApprovalAsync(arguments, cancellationToken),
            _ => Task.FromException<object>(new McpToolInputException("invalid_tool", $"Unknown tool '{name}'."))
        };

    private Task<object> HandleListOpenWorkbooksAsync(CancellationToken cancellationToken) =>
        ExecuteAsObjectAsync(_workbookServices.ListOpenWorkbooksAsync(cancellationToken));

    private Task<object> HandleConnectWorkbookAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = new WorkbookConnectionRequest(
            WorkbookPath: GetOptionalString(arguments, "workbookPath"),
            WorkbookName: GetOptionalString(arguments, "workbookName"));
        return ExecuteAsObjectAsync(_workbookServices.ConnectAsync(request, cancellationToken));
    }

    private Task<object> HandleCreateWorkbookAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = new WorkbookCreateRequest(
            WorkbookPath: GetRequiredString(arguments, "workbookPath"));
        return ExecuteAsObjectAsync(_workbookServices.CreateWorkbookAsync(request, cancellationToken));
    }

    private Task<object> HandleListConnectionsAsync(CancellationToken cancellationToken) =>
        ExecuteAsObjectAsync(_workbookServices.ListConnectionsAsync(cancellationToken));

    private Task<object> HandleGetConnectionAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var connectionId = GetRequiredString(arguments, "connectionId");
        return ExecuteAsObjectAsync(_workbookServices.GetConnectionAsync(connectionId, cancellationToken));
    }

    private Task<object> HandleDisconnectWorkbookAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var connectionId = GetRequiredString(arguments, "connectionId");
        return ExecuteAsObjectAsync(_workbookServices.DisconnectAsync(connectionId, cancellationToken));
    }

    private async Task<object> HandleListInventoryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ListInventoryAsync(resolved.WorkbookPath, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleListNamesAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ListNamesAsync(resolved.WorkbookPath, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleGetQueryAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var queryName = GetRequiredString(arguments, "queryName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.GetQueryAsync(resolved.WorkbookPath, queryName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleGetNameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var name = GetRequiredString(arguments, "name");
        var sheetName = GetOptionalString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.GetNameAsync(resolved.WorkbookPath, name, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleReadNameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var name = GetRequiredString(arguments, "name");
        var sheetName = GetOptionalString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReadNamedRangeAsync(resolved.WorkbookPath, name, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleCreateNameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var name = GetRequiredString(arguments, "name");
        var refersTo = GetRequiredString(arguments, "refersTo");
        var sheetName = GetOptionalString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.CreateNameAsync(resolved.WorkbookPath, name, refersTo, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleUpdateNameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var name = GetRequiredString(arguments, "name");
        var refersTo = GetRequiredString(arguments, "refersTo");
        var sheetName = GetOptionalString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.UpdateNameAsync(resolved.WorkbookPath, name, refersTo, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleDeleteNameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var name = GetRequiredString(arguments, "name");
        var sheetName = GetOptionalString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.DeleteNameAsync(resolved.WorkbookPath, name, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRefreshAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var queryName = GetRequiredString(arguments, "queryName");
        var options = new RefreshOptions(
            Silent: GetOptionalBoolean(arguments, "silent") ?? true,
            PreferSynchronousTableRefresh: GetOptionalBoolean(arguments, "preferSynchronousTableRefresh") ?? true,
            Timeout: GetOptionalInt32(arguments, "timeoutMs") is int timeoutMs ? TimeSpan.FromMilliseconds(timeoutMs) : null);

        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.RefreshQueryAsync(resolved.WorkbookPath, queryName, options, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleProbeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var queryName = GetRequiredString(arguments, "queryName");
        var tempPrefix = GetOptionalString(arguments, "tempPrefix") ?? "tmp_probe_mcp";
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.TryRunQueryAsync(resolved.WorkbookPath, queryName, tempPrefix, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleCleanupAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var pattern = GetRequiredString(arguments, "pattern");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.CleanupTempQueriesAsync(resolved.WorkbookPath, pattern, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleSetQueryFormulaAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var queryName = GetRequiredString(arguments, "queryName");
        var formula = GetRequiredString(arguments, "formula");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.SetQueryFormulaAsync(resolved.WorkbookPath, queryName, formula, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableReadAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var tableName = GetRequiredString(arguments, "tableName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReadTableAsync(resolved.WorkbookPath, tableName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableGetAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var tableName = GetRequiredString(arguments, "tableName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.GetTableAsync(resolved.WorkbookPath, tableName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableCreateAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new TableCreateRequest(
            TableName: GetRequiredString(arguments, "tableName"),
            SheetName: GetRequiredString(arguments, "sheetName"),
            Address: GetRequiredString(arguments, "address"),
            HasHeaders: GetOptionalBoolean(arguments, "hasHeaders") ?? true);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.CreateTableAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableResizeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new TableResizeRequest(
            TableName: GetRequiredString(arguments, "tableName"),
            SheetName: GetRequiredString(arguments, "sheetName"),
            Address: GetRequiredString(arguments, "address"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ResizeTableAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableAppendRowsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new TableRowsWriteRequest(
            TableName: GetRequiredString(arguments, "tableName"),
            Values: GetRequiredMatrix(arguments, "values"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.AppendTableRowsAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableReplaceRowsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new TableRowsWriteRequest(
            TableName: GetRequiredString(arguments, "tableName"),
            Values: GetRequiredMatrix(arguments, "values"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReplaceTableRowsAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleTableSetOptionsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var hasHeaders = GetOptionalBoolean(arguments, "hasHeaders");
        var showTotals = GetOptionalBoolean(arguments, "showTotals");
        if (hasHeaders is null && showTotals is null)
        {
            throw new McpToolInputException("invalid_arguments", "At least one of 'hasHeaders' or 'showTotals' is required.");
        }

        var request = new TableOptionsUpdateRequest(
            TableName: GetRequiredString(arguments, "tableName"),
            HasHeaders: hasHeaders,
            ShowTotals: showTotals);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.SetTableOptionsAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeReadAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        var address = GetRequiredString(arguments, "address");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReadRangeAsync(resolved.WorkbookPath, sheetName, address, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeWriteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetRangeWriteRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.WriteRangesAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<object> HandleGrantApprovalAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var ttl = GetOptionalInt32(arguments, "ttlMinutes") is int ttlMinutes
            ? TimeSpan.FromMinutes(ttlMinutes)
            : (TimeSpan?)null;

        return ExecuteAsObjectAsync(_workbookServices.GrantAttachedMutationApprovalAsync(target, ttl, cancellationToken));
    }

    private Task<object> HandleRevokeApprovalAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        return ExecuteAsObjectAsync(_workbookServices.RevokeAttachedMutationApprovalAsync(target, cancellationToken));
    }

    private static WorkbookTarget GetWorkbookTarget(JsonElement arguments) =>
        new(GetOptionalString(arguments, "workbookPath"), GetOptionalString(arguments, "connectionId"));

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

    private static JsonElement BuildTargetSchema(
        string[] requiredProperties,
        params (string Name, object Schema)[] properties)
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["workbookPath"] = new { type = "string" },
            ["connectionId"] = new { type = "string" }
        };

        foreach (var (name, schema) in properties)
        {
            dictionary[name] = schema;
        }

        return ToJsonElement(new
        {
            type = "object",
            properties = dictionary,
            required = requiredProperties
        });
    }

    private static JsonElement BuildTargetSchema(params (string Name, object Schema)[] properties) =>
        BuildTargetSchema(Array.Empty<string>(), properties);

    private static JsonElement ToJsonElement(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions);

    private static string[] GetArgumentKeys(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return arguments.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

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

    private static object?[,] GetRequiredMatrix(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property))
        {
            return ParseMatrix(property);
        }

        throw new McpToolInputException("invalid_arguments", $"Missing required matrix argument '{propertyName}'.");
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

    private static async Task<object> ExecuteAsObjectAsync<T>(Task<T> task) =>
        (object)(await task.ConfigureAwait(false))!;

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

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default)
        {
            var workbookPath = target.WorkbookPath;
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new WorkbookTargetResolutionException(
                    "workbook_target_required",
                    "This tool requires 'workbookPath' when the server is using a shared workbook service.");
            }

            return action(new ResolvedWorkbookContext(workbookPath, target.ConnectionId, _workbookService));
        }

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookSummary>>(Array.Empty<WorkbookSummary>());

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionResult>(new WorkbookTargetResolutionException(
                "connection_not_supported",
                "Workbook connections are not available on a shared workbook service resolver."));

        public Task<WorkbookConnectionResult> CreateWorkbookAsync(WorkbookCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionResult>(new WorkbookTargetResolutionException(
                "connection_not_supported",
                "Workbook connections are not available on a shared workbook service resolver."));

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(Array.Empty<WorkbookConnectionInfo>());

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionInfo>(new WorkbookTargetResolutionException(
                "connection_not_found",
                $"No workbook connection with id '{connectionId}' exists."));

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookDisconnectResult(true, connectionId, string.Empty, false));

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(new AttachedMutationApprovalModeException(
                "attached_session_approval_not_applicable",
                "Attached-session mutation approval is not available on a shared workbook service resolver.",
                "Use the host-owned workbook service resolver with an attached workbook target."));

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(new AttachedMutationApprovalModeException(
                "attached_session_approval_not_applicable",
                "Attached-session mutation approval is not available on a shared workbook service resolver.",
                "Use the host-owned workbook service resolver with an attached workbook target."));
    }
}
