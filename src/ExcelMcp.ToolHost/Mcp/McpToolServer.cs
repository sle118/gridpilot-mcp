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
            ToolNames.WorkbookSave,
            "Save the targeted workbook in place.",
            BuildTargetSchema()),
        new(
            ToolNames.WorkbookSaveAs,
            "Save the targeted workbook to a new full path and retarget the connection when applicable.",
            BuildTargetSchema(
                ["newWorkbookPath"],
                ("newWorkbookPath", new { type = "string" }))),
        new(
            ToolNames.WorkbookListInventory,
            "List workbook sheets, tables, connections, and queries.",
            BuildTargetSchema()),
        new(
            ToolNames.WorkbookListNames,
            "List workbook and worksheet-scoped Excel names.",
            BuildTargetSchema()),
        new(
            ToolNames.WorksheetCreate,
            "Create a new worksheet at the end of the workbook.",
            BuildTargetSchema(
                ["sheetName"],
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.WorksheetRename,
            "Rename an existing worksheet by exact current sheet name.",
            BuildTargetSchema(
                ["sheetName", "newSheetName"],
                ("sheetName", new { type = "string" }),
                ("newSheetName", new { type = "string" }))),
        new(
            ToolNames.WorksheetDelete,
            "Delete an existing worksheet by exact sheet name.",
            BuildTargetSchema(
                ["sheetName"],
                ("sheetName", new { type = "string" }))),
        new(
            ToolNames.WorksheetMove,
            "Move an existing worksheet within the workbook by relative placement.",
            BuildTargetSchema(
                ["sheetName"],
                ("sheetName", new { type = "string" }),
                ("beforeSheetName", new { type = "string" }),
                ("afterSheetName", new { type = "string" }),
                ("position", new { type = "string" }))),
        new(
            ToolNames.WorksheetCopy,
            "Copy an existing worksheet within the same workbook and optionally reposition it.",
            BuildTargetSchema(
                ["sheetName", "newSheetName"],
                ("sheetName", new { type = "string" }),
                ("newSheetName", new { type = "string" }),
                ("beforeSheetName", new { type = "string" }),
                ("afterSheetName", new { type = "string" }),
                ("position", new { type = "string" }))),
        new(
            ToolNames.WorksheetSetVisibility,
            "Set worksheet visibility using Excel visible, hidden, or veryHidden states.",
            BuildTargetSchema(
                ["sheetName", "visibility"],
                ("sheetName", new { type = "string" }),
                ("visibility", new { type = "string" }))),
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
            ToolNames.TableDelete,
            "Delete one Excel table by exact table name.",
            BuildTargetSchema(
                ["tableName"],
                ("tableName", new { type = "string" }))),
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
            ToolNames.RangeGetFormat,
            "Read compact formatting state for one rectangular workbook range.",
            BuildTargetSchema(
                ["sheetName", "address"],
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.RangeSetFormat,
            "Write formatting patches into one or more rectangular workbook ranges.",
            BuildTargetSchema(
                ["writes"],
                ("writes", new
                {
                    type = "array",
                    items = BuildRangeFormatWriteItemSchema()
                }))),
        new(
            ToolNames.RangeAutofit,
            "Autofit rows, columns, or both for one or more workbook range targets.",
            BuildTargetSchema(
                ["targets"],
                ("targets", new
                {
                    type = "array",
                    items = BuildRangeAutofitTargetSchema()
                }))),
        new(
            ToolNames.RangeGetFormulas,
            "Read formulas from one rectangular workbook range, returning null for non-formula cells.",
            BuildTargetSchema(
                ["sheetName", "address"],
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.RangeSetFormulas,
            "Write one or more rectangular workbook formula ranges.",
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
                            formulas = new { type = "array", items = new { type = "array" } }
                        },
                        required = new[] { "sheetName", "address", "formulas" }
                    }
                }))),
        new(
            ToolNames.RangeClear,
            "Clear contents from one or more workbook ranges while preserving formatting and layout.",
            BuildTargetSchema(
                ["clears"],
                ("clears", new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetName = new { type = "string" },
                            address = new { type = "string" }
                        },
                        required = new[] { "sheetName", "address" }
                    }
                }))),
        new(
            ToolNames.CalculationRecalculate,
            "Recalculate workbook, worksheet, or range targets without implicitly saving the workbook.",
            BuildTargetSchema(
                ["scope"],
                ("scope", new { type = "string" }),
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.CalculationInspectErrors,
            "Inspect formula and cell error state for workbook, worksheet, or range targets.",
            BuildTargetSchema(
                ["scope"],
                ("scope", new { type = "string" }),
                ("sheetName", new { type = "string" }),
                ("address", new { type = "string" }))),
        new(
            ToolNames.SessionGrantMutationPermission,
            "Grant workbook-scoped or session-scoped mutation permission for the current GridPilot host session.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    scope = new { type = "string" },
                    workbookPath = new { type = "string" },
                    connectionId = new { type = "string" },
                    ttlMinutes = new { type = "integer" }
                },
                required = new[] { "scope" }
            })),
        new(
            ToolNames.SessionRevokeMutationPermission,
            "Revoke workbook-scoped or session-scoped mutation permission for the current GridPilot host session.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    scope = new { type = "string" },
                    workbookPath = new { type = "string" },
                    connectionId = new { type = "string" }
                },
                required = new[] { "scope" }
            })),
        new(
            ToolNames.SessionGetMutationPermission,
            "Get effective workbook-scoped or session-scoped mutation permission for the current GridPilot host session.",
            ToJsonElement(new
            {
                type = "object",
                properties = new
                {
                    scope = new { type = "string" },
                    workbookPath = new { type = "string" },
                    connectionId = new { type = "string" }
                },
                required = new[] { "scope" }
            })),
        new(
            ToolNames.AttachedSessionGrantMutation,
            "Deprecated compatibility shim: grant workbook-scoped mutation permission.",
            BuildTargetSchema(
                ("ttlMinutes", new { type = "integer" }))),
        new(
            ToolNames.AttachedSessionRevokeMutation,
            "Deprecated compatibility shim: revoke workbook-scoped mutation permission.",
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
            ToolNames.WorkbookSave => HandleWorkbookSaveAsync(arguments, cancellationToken),
            ToolNames.WorkbookSaveAs => HandleWorkbookSaveAsAsync(arguments, cancellationToken),
            ToolNames.WorkbookListInventory => HandleListInventoryAsync(arguments, cancellationToken),
            ToolNames.WorkbookListNames => HandleListNamesAsync(arguments, cancellationToken),
            ToolNames.WorksheetCreate => HandleWorksheetCreateAsync(arguments, cancellationToken),
            ToolNames.WorksheetRename => HandleWorksheetRenameAsync(arguments, cancellationToken),
            ToolNames.WorksheetDelete => HandleWorksheetDeleteAsync(arguments, cancellationToken),
            ToolNames.WorksheetMove => HandleWorksheetMoveAsync(arguments, cancellationToken),
            ToolNames.WorksheetCopy => HandleWorksheetCopyAsync(arguments, cancellationToken),
            ToolNames.WorksheetSetVisibility => HandleWorksheetSetVisibilityAsync(arguments, cancellationToken),
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
            ToolNames.TableDelete => HandleTableDeleteAsync(arguments, cancellationToken),
            ToolNames.RangeRead => HandleRangeReadAsync(arguments, cancellationToken),
            ToolNames.RangeWrite => HandleRangeWriteAsync(arguments, cancellationToken),
            ToolNames.RangeGetFormat => HandleRangeGetFormatAsync(arguments, cancellationToken),
            ToolNames.RangeSetFormat => HandleRangeSetFormatAsync(arguments, cancellationToken),
            ToolNames.RangeAutofit => HandleRangeAutofitAsync(arguments, cancellationToken),
            ToolNames.RangeGetFormulas => HandleRangeGetFormulasAsync(arguments, cancellationToken),
            ToolNames.RangeSetFormulas => HandleRangeSetFormulasAsync(arguments, cancellationToken),
            ToolNames.RangeClear => HandleRangeClearAsync(arguments, cancellationToken),
            ToolNames.CalculationRecalculate => HandleCalculationRecalculateAsync(arguments, cancellationToken),
            ToolNames.CalculationInspectErrors => HandleCalculationInspectErrorsAsync(arguments, cancellationToken),
            ToolNames.SessionGrantMutationPermission => HandleGrantMutationPermissionAsync(arguments, cancellationToken),
            ToolNames.SessionRevokeMutationPermission => HandleRevokeMutationPermissionAsync(arguments, cancellationToken),
            ToolNames.SessionGetMutationPermission => HandleGetMutationPermissionAsync(arguments, cancellationToken),
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

    private Task<object> HandleWorkbookSaveAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        return ExecuteAsObjectAsync(_workbookServices.SaveWorkbookAsync(target, cancellationToken));
    }

    private Task<object> HandleWorkbookSaveAsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = new WorkbookSaveAsRequest(
            WorkbookPath: GetOptionalString(arguments, "workbookPath"),
            ConnectionId: GetOptionalString(arguments, "connectionId"),
            NewWorkbookPath: GetRequiredString(arguments, "newWorkbookPath"));
        return ExecuteAsObjectAsync(_workbookServices.SaveWorkbookAsAsync(request, cancellationToken));
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

    private async Task<object> HandleWorksheetCreateAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.CreateWorksheetAsync(resolved.WorkbookPath, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleWorksheetRenameAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        var newSheetName = GetRequiredString(arguments, "newSheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.RenameWorksheetAsync(resolved.WorkbookPath, sheetName, newSheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleWorksheetDeleteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.DeleteWorksheetAsync(resolved.WorkbookPath, sheetName, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleWorksheetMoveAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new WorksheetMoveRequest(
            SheetName: GetRequiredString(arguments, "sheetName"),
            BeforeSheetName: GetOptionalString(arguments, "beforeSheetName"),
            AfterSheetName: GetOptionalString(arguments, "afterSheetName"),
            Position: GetOptionalString(arguments, "position"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.MoveWorksheetAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleWorksheetCopyAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new WorksheetCopyRequest(
            SheetName: GetRequiredString(arguments, "sheetName"),
            NewSheetName: GetRequiredString(arguments, "newSheetName"),
            BeforeSheetName: GetOptionalString(arguments, "beforeSheetName"),
            AfterSheetName: GetOptionalString(arguments, "afterSheetName"),
            Position: GetOptionalString(arguments, "position"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.CopyWorksheetAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleWorksheetSetVisibilityAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = new WorksheetVisibilityRequest(
            SheetName: GetRequiredString(arguments, "sheetName"),
            Visibility: GetRequiredString(arguments, "visibility"));
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.SetWorksheetVisibilityAsync(resolved.WorkbookPath, request, cancellationToken),
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

    private async Task<object> HandleTableDeleteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var tableName = GetRequiredString(arguments, "tableName");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.DeleteTableAsync(resolved.WorkbookPath, tableName, cancellationToken),
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

    private async Task<object> HandleRangeGetFormatAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        var address = GetRequiredString(arguments, "address");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReadRangeFormatAsync(resolved.WorkbookPath, sheetName, address, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeSetFormatAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetRangeFormatWriteRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.WriteRangeFormatsAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeAutofitAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetRangeAutofitRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.AutofitRangesAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeGetFormulasAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var sheetName = GetRequiredString(arguments, "sheetName");
        var address = GetRequiredString(arguments, "address");
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ReadRangeFormulasAsync(resolved.WorkbookPath, sheetName, address, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeSetFormulasAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetRangeFormulaWriteRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.WriteRangeFormulasAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleRangeClearAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetRangeClearRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.ClearRangesAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleCalculationRecalculateAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetCalculationRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.RecalculateAsync(resolved.WorkbookPath, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<object> HandleCalculationInspectErrorsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var target = GetWorkbookTarget(arguments);
        var request = GetErrorInspectionRequest(arguments);
        return await _workbookServices.ExecuteAsync(
            target,
            resolved => resolved.Service.InspectErrorsAsync(resolved.WorkbookPath, request, cancellationToken),
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

    private Task<object> HandleGrantMutationPermissionAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var ttl = GetOptionalInt32(arguments, "ttlMinutes") is int ttlMinutes
            ? TimeSpan.FromMinutes(ttlMinutes)
            : (TimeSpan?)null;
        var request = new MutationPermissionGrantRequest(
            Scope: GetRequiredString(arguments, "scope"),
            WorkbookPath: GetOptionalString(arguments, "workbookPath"),
            ConnectionId: GetOptionalString(arguments, "connectionId"));
        return ExecuteAsObjectAsync(_workbookServices.GrantMutationPermissionAsync(request, ttl, cancellationToken));
    }

    private Task<object> HandleRevokeMutationPermissionAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = new MutationPermissionRevokeRequest(
            Scope: GetRequiredString(arguments, "scope"),
            WorkbookPath: GetOptionalString(arguments, "workbookPath"),
            ConnectionId: GetOptionalString(arguments, "connectionId"));
        return ExecuteAsObjectAsync(_workbookServices.RevokeMutationPermissionAsync(request, cancellationToken));
    }

    private Task<object> HandleGetMutationPermissionAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = new MutationPermissionStatusRequest(
            Scope: GetRequiredString(arguments, "scope"),
            WorkbookPath: GetOptionalString(arguments, "workbookPath"),
            ConnectionId: GetOptionalString(arguments, "connectionId"));
        return ExecuteAsObjectAsync(_workbookServices.GetMutationPermissionStatusAsync(request, cancellationToken));
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

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            return property.GetDouble();
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

    private static object BuildRangeFormatWriteItemSchema() =>
        new
        {
            type = "object",
            properties = new
            {
                sheetName = new { type = "string" },
                address = new { type = "string" },
                format = BuildRangeFormatPatchSchema()
            },
            required = new[] { "sheetName", "address", "format" }
        };

    private static object BuildRangeAutofitTargetSchema() =>
        new
        {
            type = "object",
            properties = new
            {
                sheetName = new { type = "string" },
                address = new { type = "string" },
                dimension = new { type = "string" }
            },
            required = new[] { "sheetName", "address", "dimension" }
        };

    private static object BuildRangeFormatPatchSchema() =>
        new
        {
            type = "object",
            properties = new
            {
                numberFormat = new { type = "string" },
                fontName = new { type = "string" },
                fontSize = new { type = "number" },
                bold = new { type = "boolean" },
                italic = new { type = "boolean" },
                fontColor = new { type = "string" },
                hasFill = new { type = "boolean" },
                fillColor = new { type = "string" },
                horizontalAlignment = new { type = "string" },
                verticalAlignment = new { type = "string" },
                wrapText = new { type = "boolean" },
                rowHeight = new { type = "number" },
                columnWidth = new { type = "number" }
            }
        };

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

    private static RangeFormatWriteRequest GetRangeFormatWriteRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("writes", out var writesElement) ||
            writesElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "Missing required array argument 'writes'.");
        }

        var writes = new List<RangeFormatWriteTarget>();
        foreach (var write in writesElement.EnumerateArray())
        {
            if (write.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each 'writes' item must be an object.");
            }

            if (!write.TryGetProperty("format", out var formatElement) || formatElement.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each range format write must include a 'format' object.");
            }

            writes.Add(new RangeFormatWriteTarget(
                GetRequiredString(write, "sheetName"),
                GetRequiredString(write, "address"),
                ParseRangeFormatPatch(formatElement)));
        }

        if (writes.Count == 0)
        {
            throw new McpToolInputException("invalid_arguments", "At least one range format write target is required.");
        }

        return new RangeFormatWriteRequest(writes);
    }

    private static RangeAutofitRequest GetRangeAutofitRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("targets", out var targetsElement) ||
            targetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "Missing required array argument 'targets'.");
        }

        var targets = new List<RangeAutofitTarget>();
        foreach (var target in targetsElement.EnumerateArray())
        {
            if (target.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each 'targets' item must be an object.");
            }

            targets.Add(new RangeAutofitTarget(
                GetRequiredString(target, "sheetName"),
                GetRequiredString(target, "address"),
                GetRequiredString(target, "dimension")));
        }

        if (targets.Count == 0)
        {
            throw new McpToolInputException("invalid_arguments", "At least one autofit target is required.");
        }

        return new RangeAutofitRequest(targets);
    }

    private static RangeFormulaWriteRequest GetRangeFormulaWriteRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("writes", out var writesElement) ||
            writesElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "Missing required array argument 'writes'.");
        }

        var writes = new List<RangeFormulaWriteTarget>();
        foreach (var write in writesElement.EnumerateArray())
        {
            if (write.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each 'writes' item must be an object.");
            }

            var sheetName = GetRequiredString(write, "sheetName");
            var address = GetRequiredString(write, "address");
            if (!write.TryGetProperty("formulas", out var formulasElement))
            {
                throw new McpToolInputException("invalid_arguments", "Each range formula write must include 'formulas'.");
            }

            writes.Add(new RangeFormulaWriteTarget(sheetName, address, ParseStringMatrix(formulasElement)));
        }

        if (writes.Count == 0)
        {
            throw new McpToolInputException("invalid_arguments", "At least one range formula write target is required.");
        }

        return new RangeFormulaWriteRequest(writes);
    }

    private static RangeClearRequest GetRangeClearRequest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("clears", out var clearsElement) ||
            clearsElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "Missing required array argument 'clears'.");
        }

        var clears = new List<RangeClearTarget>();
        foreach (var clear in clearsElement.EnumerateArray())
        {
            if (clear.ValueKind != JsonValueKind.Object)
            {
                throw new McpToolInputException("invalid_arguments", "Each 'clears' item must be an object.");
            }

            clears.Add(new RangeClearTarget(
                GetRequiredString(clear, "sheetName"),
                GetRequiredString(clear, "address")));
        }

        if (clears.Count == 0)
        {
            throw new McpToolInputException("invalid_arguments", "At least one range clear target is required.");
        }

        return new RangeClearRequest(clears);
    }

    private static CalculationRequest GetCalculationRequest(JsonElement element)
    {
        var scope = GetRequiredString(element, "scope");
        var sheetName = GetOptionalString(element, "sheetName");
        var address = GetOptionalString(element, "address");
        ValidateScopedTargetArguments(scope, sheetName, address);
        return new CalculationRequest(scope, sheetName, address);
    }

    private static ErrorInspectionRequest GetErrorInspectionRequest(JsonElement element)
    {
        var scope = GetRequiredString(element, "scope");
        var sheetName = GetOptionalString(element, "sheetName");
        var address = GetOptionalString(element, "address");
        ValidateScopedTargetArguments(scope, sheetName, address);
        return new ErrorInspectionRequest(scope, sheetName, address);
    }

    private static RangeFormatPatch ParseRangeFormatPatch(JsonElement element)
    {
        var patch = new RangeFormatPatch(
            NumberFormat: GetOptionalString(element, "numberFormat"),
            FontName: GetOptionalString(element, "fontName"),
            FontSize: GetOptionalDouble(element, "fontSize"),
            Bold: GetOptionalBoolean(element, "bold"),
            Italic: GetOptionalBoolean(element, "italic"),
            FontColor: GetOptionalString(element, "fontColor"),
            HasFill: GetOptionalBoolean(element, "hasFill"),
            FillColor: GetOptionalString(element, "fillColor"),
            HorizontalAlignment: GetOptionalString(element, "horizontalAlignment"),
            VerticalAlignment: GetOptionalString(element, "verticalAlignment"),
            WrapText: GetOptionalBoolean(element, "wrapText"),
            RowHeight: GetOptionalDouble(element, "rowHeight"),
            ColumnWidth: GetOptionalDouble(element, "columnWidth"));

        if (patch.IsEmpty)
        {
            throw new McpToolInputException("invalid_arguments", "Each range format patch must include at least one formatting property.");
        }

        return patch;
    }

    private static void ValidateScopedTargetArguments(string scope, string? sheetName, string? address)
    {
        switch (scope.Trim().ToLowerInvariant())
        {
            case "workbook":
                return;
            case "worksheet":
                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    throw new McpToolInputException("invalid_arguments", "Worksheet scope requires 'sheetName'.");
                }

                return;
            case "range":
                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    throw new McpToolInputException("invalid_arguments", "Range scope requires 'sheetName'.");
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    throw new McpToolInputException("invalid_arguments", "Range scope requires 'address'.");
                }

                return;
            default:
                throw new McpToolInputException("invalid_arguments", "Scope must be one of 'workbook', 'worksheet', or 'range'.");
        }
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

    private static string?[,] ParseStringMatrix(JsonElement valuesElement)
    {
        if (valuesElement.ValueKind != JsonValueKind.Array)
        {
            throw new McpToolInputException("invalid_arguments", "'formulas' must be a rectangular array of arrays.");
        }

        var rows = valuesElement.EnumerateArray().ToArray();
        if (rows.Length == 0)
        {
            throw new McpToolInputException("invalid_arguments", "'formulas' must contain at least one row.");
        }

        if (rows.Any(row => row.ValueKind != JsonValueKind.Array))
        {
            throw new McpToolInputException("invalid_arguments", "'formulas' must be a rectangular array of arrays.");
        }

        var columnCount = rows[0].GetArrayLength();
        if (columnCount == 0)
        {
            throw new McpToolInputException("invalid_arguments", "'formulas' rows must contain at least one column.");
        }

        if (rows.Any(row => row.GetArrayLength() != columnCount))
        {
            throw new McpToolInputException("invalid_arguments", "'formulas' must be rectangular.");
        }

        var matrix = new string?[rows.Length, columnCount];
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var cells = rows[rowIndex].EnumerateArray().ToArray();
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                matrix[rowIndex, columnIndex] = ParseFormulaValue(cells[columnIndex]);
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

    private static string? ParseFormulaValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            _ => throw new McpToolInputException("invalid_arguments", "Range formula cells must be strings or null.")
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

        public Task<WorkbookSaveResult> SaveWorkbookAsync(WorkbookTarget target, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(target.WorkbookPath))
            {
                throw new WorkbookTargetResolutionException(
                    "workbook_target_required",
                    "This tool requires 'workbookPath' when the server is using a shared workbook service.");
            }

            return _workbookService.SaveWorkbookAsync(target.WorkbookPath!, target.ConnectionId, cancellationToken);
        }

        public Task<WorkbookSaveResult> SaveWorkbookAsAsync(WorkbookSaveAsRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.WorkbookPath))
            {
                throw new WorkbookTargetResolutionException(
                    "workbook_target_required",
                    "This tool requires 'workbookPath' when the server is using a shared workbook service.");
            }

            return _workbookService.SaveWorkbookAsAsync(request.WorkbookPath!, request.NewWorkbookPath, request.ConnectionId, cancellationToken);
        }

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(Array.Empty<WorkbookConnectionInfo>());

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionInfo>(new WorkbookTargetResolutionException(
                "connection_not_found",
                $"No workbook connection with id '{connectionId}' exists."));

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookDisconnectResult(true, connectionId, string.Empty, false));

        public Task<MutationPermissionGrantResult> GrantMutationPermissionAsync(MutationPermissionGrantRequest request, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<MutationPermissionGrantResult>(new WorkbookTargetResolutionException(
                "connection_not_supported",
                "Mutation permission is not available on a shared workbook service resolver."));

        public Task<MutationPermissionRevokeResult> RevokeMutationPermissionAsync(MutationPermissionRevokeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<MutationPermissionRevokeResult>(new WorkbookTargetResolutionException(
                "connection_not_supported",
                "Mutation permission is not available on a shared workbook service resolver."));

        public Task<MutationPermissionStatusResult> GetMutationPermissionStatusAsync(MutationPermissionStatusRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<MutationPermissionStatusResult>(new WorkbookTargetResolutionException(
                "connection_not_supported",
                "Mutation permission is not available on a shared workbook service resolver."));

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
