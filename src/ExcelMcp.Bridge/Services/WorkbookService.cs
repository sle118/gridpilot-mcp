using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;
using System.Diagnostics;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookService
{
    private readonly IExcelSession _session;
    private readonly WorkbookOperationSafety _operationSafety;
    private readonly IGridPilotLogger _logger;
    private static readonly SessionOptions QuietSessionOptions = new(
        DisplayAlerts: false,
        ScreenUpdating: false,
        EnableEvents: false);

    public WorkbookService(IExcelSession session, WorkbookOperationSafety? operationSafety = null, IGridPilotLogger? logger = null)
    {
        _session = session;
        _logger = logger ?? GridPilotNullLogger.Instance;
        _operationSafety = operationSafety ?? new WorkbookOperationSafety(session, logger: _logger);
    }

    public async Task<QueryDefinition> GetQueryAsync(string workbookPath, string queryName, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetQueryAsync(queryName, cancellationToken);
    }

    public async Task<QueryDetail> GetQueryDetailAsync(string workbookPath, string queryName, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetQueryDetailAsync(queryName, cancellationToken);
    }

    public async Task<ConnectionDetail> GetConnectionAsync(string workbookPath, string connectionName, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetConnectionAsync(connectionName, cancellationToken);
    }

    public async Task<WorkbookDependencyGraph> GetDependencyGraphAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetDependencyGraphAsync(cancellationToken);
    }

    public async Task<WorkbookStructureState> GetWorkbookStructureStateAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetWorkbookStructureStateAsync(cancellationToken);
    }

    public async Task<WorkbookProtectionState> GetWorkbookProtectionStateAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetWorkbookProtectionStateAsync(cancellationToken);
    }

    public async Task<QueryMutationResult> CreateQueryAsync(
        string workbookPath,
        QueryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateQueryCreateRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new QueryMutationResult(false, workbookPath, request.QueryName, "create", LoadMode: NormalizeOptional(request.LoadMode), DestinationSheetName: request.DestinationSheetName, DestinationAddress: request.DestinationAddress, Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new QueryMutationResult(false, workbookPath, validatedRequest.QueryName, "create", LoadMode: validatedRequest.LoadMode, DestinationSheetName: validatedRequest.DestinationSheetName, DestinationAddress: validatedRequest.DestinationAddress, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CreateQueryAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            QueryDetail? detail = null;
            try
            {
                detail = await workbook.GetQueryDetailAsync(validatedRequest.QueryName, cancellationToken);
            }
            catch
            {
            }

            _logger.LogInfo(nameof(WorkbookService), "query_created", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = validatedRequest.QueryName,
                ["loadMode"] = validatedRequest.LoadMode
            });
            return new QueryMutationResult(
                true,
                workbookPath,
                validatedRequest.QueryName,
                "create",
                LoadMode: detail?.LoadMode ?? validatedRequest.LoadMode,
                DestinationSheetName: detail?.DestinationSheetName ?? validatedRequest.DestinationSheetName,
                DestinationAddress: detail?.DestinationAddress ?? validatedRequest.DestinationAddress,
                ConnectionName: detail?.ConnectionName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "query_create_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = validatedRequest.QueryName
            }, ex);
            return BuildQueryMutationError(workbookPath, validatedRequest.QueryName, "create", null, validatedRequest.LoadMode, validatedRequest.DestinationSheetName, validatedRequest.DestinationAddress, null, ex);
        }
    }

    public async Task<QueryMutationResult> RenameQueryAsync(
        string workbookPath,
        QueryRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateQueryRenameRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new QueryMutationResult(false, workbookPath, request.QueryName, "rename", request.NewQueryName, Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new QueryMutationResult(false, workbookPath, validatedRequest.QueryName, "rename", validatedRequest.NewQueryName, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            QueryDetail? detail = null;
            try
            {
                detail = await workbook.GetQueryDetailAsync(validatedRequest.QueryName, cancellationToken);
            }
            catch
            {
            }

            await workbook.RenameQueryAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "query_renamed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = validatedRequest.QueryName,
                ["newQueryName"] = validatedRequest.NewQueryName
            });
            return new QueryMutationResult(
                true,
                workbookPath,
                validatedRequest.QueryName,
                "rename",
                validatedRequest.NewQueryName,
                detail?.LoadMode,
                detail?.DestinationSheetName,
                detail?.DestinationAddress,
                detail?.ConnectionName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "query_rename_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = validatedRequest.QueryName,
                ["newQueryName"] = validatedRequest.NewQueryName
            }, ex);
            return BuildQueryMutationError(workbookPath, validatedRequest.QueryName, "rename", validatedRequest.NewQueryName, null, null, null, null, ex);
        }
    }

    public async Task<QueryDeleteResult> DeleteQueryAsync(
        string workbookPath,
        string queryName,
        CancellationToken cancellationToken = default)
    {
        var normalizedQueryName = NormalizeOptional(queryName);
        if (normalizedQueryName is null)
        {
            return new QueryDeleteResult(false, workbookPath, queryName, Error: new OperationError("query_delete_invalid", "Query delete requires a non-empty query name.", "Provide 'queryName'.", nameof(WorkbookService)));
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new QueryDeleteResult(false, workbookPath, normalizedQueryName, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            string? connectionName = null;
            try
            {
                connectionName = (await workbook.GetQueryDetailAsync(normalizedQueryName, cancellationToken)).ConnectionName;
            }
            catch
            {
            }

            await workbook.DeleteQueryAsync(normalizedQueryName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "query_deleted", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = normalizedQueryName
            });
            return new QueryDeleteResult(true, workbookPath, normalizedQueryName, connectionName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "query_delete_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = normalizedQueryName
            }, ex);
            return new QueryDeleteResult(
                false,
                workbookPath,
                normalizedQueryName,
                Error: new OperationError(
                    Code: "query_delete_failed",
                    Message: $"Failed to delete query '{normalizedQueryName}'.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<ConnectionMutationResult> RenameConnectionAsync(
        string workbookPath,
        ConnectionRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateConnectionRenameRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new ConnectionMutationResult(false, workbookPath, request.ConnectionName, "rename", request.NewConnectionName, Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new ConnectionMutationResult(false, workbookPath, validatedRequest.ConnectionName, "rename", validatedRequest.NewConnectionName, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.RenameConnectionAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "connection_renamed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = validatedRequest.ConnectionName,
                ["newConnectionName"] = validatedRequest.NewConnectionName
            });
            return new ConnectionMutationResult(true, workbookPath, validatedRequest.ConnectionName, "rename", validatedRequest.NewConnectionName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "connection_rename_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = validatedRequest.ConnectionName,
                ["newConnectionName"] = validatedRequest.NewConnectionName
            }, ex);
            return BuildConnectionMutationError(workbookPath, validatedRequest.ConnectionName, "rename", validatedRequest.NewConnectionName, null, ex);
        }
    }

    public async Task<ConnectionMutationResult> UpdateConnectionAsync(
        string workbookPath,
        ConnectionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateConnectionUpdateRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new ConnectionMutationResult(false, workbookPath, request.ConnectionName, "update", RefreshWithRefreshAll: request.RefreshWithRefreshAll, BackgroundQuery: request.BackgroundQuery, EnableRefresh: request.EnableRefresh, RefreshOnFileOpen: request.RefreshOnFileOpen, SavePassword: request.SavePassword, Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new ConnectionMutationResult(false, workbookPath, validatedRequest.ConnectionName, "update", RefreshWithRefreshAll: validatedRequest.RefreshWithRefreshAll, BackgroundQuery: validatedRequest.BackgroundQuery, EnableRefresh: validatedRequest.EnableRefresh, RefreshOnFileOpen: validatedRequest.RefreshOnFileOpen, SavePassword: validatedRequest.SavePassword, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.UpdateConnectionAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "connection_updated", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = validatedRequest.ConnectionName
            });
            return new ConnectionMutationResult(true, workbookPath, validatedRequest.ConnectionName, "update", RefreshWithRefreshAll: validatedRequest.RefreshWithRefreshAll, BackgroundQuery: validatedRequest.BackgroundQuery, EnableRefresh: validatedRequest.EnableRefresh, RefreshOnFileOpen: validatedRequest.RefreshOnFileOpen, SavePassword: validatedRequest.SavePassword);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "connection_update_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = validatedRequest.ConnectionName
            }, ex);
            return BuildConnectionMutationError(workbookPath, validatedRequest.ConnectionName, "update", null, validatedRequest, ex);
        }
    }

    public async Task<ConnectionMutationResult> DeleteConnectionAsync(
        string workbookPath,
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        var normalizedConnectionName = NormalizeOptional(connectionName);
        if (normalizedConnectionName is null)
        {
            return new ConnectionMutationResult(false, workbookPath, connectionName, "delete", Error: new OperationError("connection_delete_invalid", "Connection delete requires a non-empty connection name.", "Provide 'connectionName'.", nameof(WorkbookService)));
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new ConnectionMutationResult(false, workbookPath, normalizedConnectionName, "delete", Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.DeleteConnectionAsync(normalizedConnectionName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "connection_deleted", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = normalizedConnectionName
            });
            return new ConnectionMutationResult(true, workbookPath, normalizedConnectionName, "delete");
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "connection_delete_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionName"] = normalizedConnectionName
            }, ex);
            return BuildConnectionMutationError(workbookPath, normalizedConnectionName, "delete", null, null, ex);
        }
    }

    public async Task<WorkbookStructureMutationResult> SetWorkbookVisibilityAsync(
        string workbookPath,
        WorkbookVisibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateWorkbookVisibilityRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new WorkbookStructureMutationResult(false, workbookPath, "set_visibility", Visibility: NormalizeOptional(request.Visibility), Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorkbookStructureMutationResult(false, workbookPath, "set_visibility", Visibility: validatedRequest.Visibility, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetWorkbookVisibilityAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "workbook_visibility_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["visibility"] = validatedRequest.Visibility
            });
            return new WorkbookStructureMutationResult(true, workbookPath, "set_visibility", Visibility: validatedRequest.Visibility);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "workbook_visibility_set_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["visibility"] = validatedRequest.Visibility
            }, ex);
            return BuildWorkbookStructureMutationError(workbookPath, "set_visibility", validatedRequest.Visibility, null, null, null, ex);
        }
    }

    public async Task<WorkbookStructureMutationResult> SetWorkbookProtectionAsync(
        string workbookPath,
        WorkbookProtectionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validatedRequest = ValidateWorkbookProtectionUpdateRequest(request, out var validationError);
        if (validatedRequest is null)
        {
            return new WorkbookStructureMutationResult(false, workbookPath, "set_protection", Mode: NormalizeOptional(request.Mode), ProtectStructure: request.ProtectStructure, ProtectWindows: request.ProtectWindows, Error: validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorkbookStructureMutationResult(false, workbookPath, "set_protection", Mode: validatedRequest.Mode, ProtectStructure: validatedRequest.ProtectStructure, ProtectWindows: validatedRequest.ProtectWindows, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetWorkbookProtectionAsync(validatedRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "workbook_protection_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["mode"] = validatedRequest.Mode
            });
            return new WorkbookStructureMutationResult(true, workbookPath, "set_protection", Mode: validatedRequest.Mode, ProtectStructure: validatedRequest.ProtectStructure, ProtectWindows: validatedRequest.ProtectWindows);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "workbook_protection_set_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["mode"] = validatedRequest.Mode
            }, ex);
            return BuildWorkbookProtectionError(workbookPath, validatedRequest, ex);
        }
    }

    public async Task<NameSummary> GetNameAsync(string workbookPath, string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetNameAsync(name, sheetName, cancellationToken);
    }

    public async Task<NameMutationResult> CreateNameAsync(
        string workbookPath,
        string name,
        string refersTo,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "create", GetScope(sheetName), sheetName, refersTo, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CreateNameAsync(name, refersTo, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "name_created", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            });
            return new NameMutationResult(true, workbookPath, name, "create", GetScope(sheetName), sheetName, refersTo);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "name_create_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            }, ex);
            return BuildNameMutationError(workbookPath, name, "create", sheetName, refersTo, ex);
        }
    }

    public async Task<NameMutationResult> UpdateNameAsync(
        string workbookPath,
        string name,
        string refersTo,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "update", GetScope(sheetName), sheetName, refersTo, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.UpdateNameAsync(name, refersTo, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "name_updated", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            });
            return new NameMutationResult(true, workbookPath, name, "update", GetScope(sheetName), sheetName, refersTo);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "name_update_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            }, ex);
            return BuildNameMutationError(workbookPath, name, "update", sheetName, refersTo, ex);
        }
    }

    public async Task<NameMutationResult> DeleteNameAsync(
        string workbookPath,
        string name,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "delete", GetScope(sheetName), sheetName, null, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.DeleteNameAsync(name, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "name_deleted", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            });
            return new NameMutationResult(true, workbookPath, name, "delete", GetScope(sheetName), sheetName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "name_delete_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["name"] = name,
                ["sheetName"] = sheetName
            }, ex);
            return BuildNameMutationError(workbookPath, name, "delete", sheetName, null, ex);
        }
    }

    public async Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListSheetsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TableSummary>> ListTablesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListTablesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListQueriesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListConnectionsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NameSummary>> ListNamesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListNamesAsync(cancellationToken);
    }

    public async Task<WorkbookInventory> ListInventoryAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var sheets = await workbook.ListSheetsAsync(cancellationToken);
        var tables = await workbook.ListTablesAsync(cancellationToken);
        var queries = await workbook.ListQueriesAsync(cancellationToken);
        var connections = await workbook.ListConnectionsAsync(cancellationToken);
        _logger.LogDebug(nameof(WorkbookService), "inventory_listed", new Dictionary<string, object?>
        {
            ["workbookPath"] = workbookPath,
            ["sheetCount"] = sheets.Count,
            ["tableCount"] = tables.Count,
            ["queryCount"] = queries.Count,
            ["connectionCount"] = connections.Count
        });
        return new WorkbookInventory(sheets, tables, queries, connections);
    }

    public async Task<WorkbookSaveResult> SaveWorkbookAsync(
        string workbookPath,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorkbookSaveResult(false, workbookPath, workbookPath, "save", connectionId, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "workbook_saved", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbook.FullPath,
                ["connectionId"] = connectionId
            });
            return new WorkbookSaveResult(true, workbookPath, workbook.FullPath, "save", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "workbook_save_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionId"] = connectionId
            }, ex);
            return BuildWorkbookSaveError(workbookPath, workbookPath, "save", connectionId, ex);
        }
    }

    public async Task<WorkbookSaveResult> SaveWorkbookAsAsync(
        string workbookPath,
        string newWorkbookPath,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorkbookSaveResult(false, workbookPath, newWorkbookPath, "save_as", connectionId, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SaveAsAsync(newWorkbookPath, cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "workbook_saved_as", new Dictionary<string, object?>
            {
                ["sourceWorkbookPath"] = workbookPath,
                ["workbookPath"] = workbook.FullPath,
                ["connectionId"] = connectionId
            });
            return new WorkbookSaveResult(true, workbookPath, workbook.FullPath, "save_as", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "workbook_save_as_failed", new Dictionary<string, object?>
            {
                ["sourceWorkbookPath"] = workbookPath,
                ["workbookPath"] = newWorkbookPath,
                ["connectionId"] = connectionId
            }, ex);
            return BuildWorkbookSaveError(workbookPath, newWorkbookPath, "save_as", connectionId, ex);
        }
    }

    public async Task<WorksheetMutationResult> CreateWorksheetAsync(
        string workbookPath,
        string sheetName,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetMutationResult(false, workbookPath, sheetName, "create", Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CreateWorksheetAsync(sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_created", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName
            });
            return new WorksheetMutationResult(true, workbookPath, sheetName, "create");
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_create_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName
            }, ex);
            return BuildWorksheetMutationError(workbookPath, sheetName, "create", null, ex);
        }
    }

    public async Task<WorksheetMutationResult> RenameWorksheetAsync(
        string workbookPath,
        string sheetName,
        string newSheetName,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetMutationResult(false, workbookPath, sheetName, "rename", newSheetName, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.RenameWorksheetAsync(sheetName, newSheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_renamed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName,
                ["newSheetName"] = newSheetName
            });
            return new WorksheetMutationResult(true, workbookPath, sheetName, "rename", newSheetName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_rename_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName,
                ["newSheetName"] = newSheetName
            }, ex);
            return BuildWorksheetMutationError(workbookPath, sheetName, "rename", newSheetName, ex);
        }
    }

    public async Task<WorksheetMutationResult> DeleteWorksheetAsync(
        string workbookPath,
        string sheetName,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetMutationResult(false, workbookPath, sheetName, "delete", Error: safetyError);
        }

        try
        {
            await using var _ = await _session.BeginScopeAsync(QuietSessionOptions, cancellationToken);
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.DeleteWorksheetAsync(sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_deleted", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName
            });
            return new WorksheetMutationResult(true, workbookPath, sheetName, "delete");
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_delete_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName
            }, ex);
            return BuildWorksheetMutationError(workbookPath, sheetName, "delete", null, ex);
        }
    }

    public async Task<WorksheetLayoutMutationResult> MoveWorksheetAsync(
        string workbookPath,
        WorksheetMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = ValidateWorksheetMoveRequest(request, out var validationError);
        if (validationError is not null)
        {
            return new WorksheetLayoutMutationResult(false, workbookPath, request.SheetName, "move", Error: validationError);
        }
        var validRequest = normalizedRequest!;

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetLayoutMutationResult(
                false,
                workbookPath,
                validRequest.SheetName,
                "move",
                BeforeSheetName: validRequest.BeforeSheetName,
                AfterSheetName: validRequest.AfterSheetName,
                Position: validRequest.Position,
                Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.MoveWorksheetAsync(validRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_moved", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName,
                ["beforeSheetName"] = validRequest.BeforeSheetName,
                ["afterSheetName"] = validRequest.AfterSheetName,
                ["position"] = validRequest.Position
            });
            return new WorksheetLayoutMutationResult(
                true,
                workbookPath,
                validRequest.SheetName,
                "move",
                BeforeSheetName: validRequest.BeforeSheetName,
                AfterSheetName: validRequest.AfterSheetName,
                Position: validRequest.Position);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_move_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName
            }, ex);
            return BuildWorksheetLayoutMutationError(workbookPath, validRequest.SheetName, "move", null, validRequest.BeforeSheetName, validRequest.AfterSheetName, validRequest.Position, null, ex);
        }
    }

    public async Task<WorksheetLayoutMutationResult> CopyWorksheetAsync(
        string workbookPath,
        WorksheetCopyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = ValidateWorksheetCopyRequest(request, out var validationError);
        if (validationError is not null)
        {
            return new WorksheetLayoutMutationResult(false, workbookPath, request.SheetName, "copy", request.NewSheetName, Error: validationError);
        }
        var validRequest = normalizedRequest!;

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetLayoutMutationResult(
                false,
                workbookPath,
                validRequest.SheetName,
                "copy",
                validRequest.NewSheetName,
                validRequest.BeforeSheetName,
                validRequest.AfterSheetName,
                validRequest.Position,
                Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CopyWorksheetAsync(validRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_copied", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName,
                ["newSheetName"] = validRequest.NewSheetName,
                ["beforeSheetName"] = validRequest.BeforeSheetName,
                ["afterSheetName"] = validRequest.AfterSheetName,
                ["position"] = validRequest.Position
            });
            return new WorksheetLayoutMutationResult(
                true,
                workbookPath,
                validRequest.SheetName,
                "copy",
                validRequest.NewSheetName,
                validRequest.BeforeSheetName,
                validRequest.AfterSheetName,
                validRequest.Position);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_copy_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName,
                ["newSheetName"] = validRequest.NewSheetName
            }, ex);
            return BuildWorksheetLayoutMutationError(workbookPath, validRequest.SheetName, "copy", validRequest.NewSheetName, validRequest.BeforeSheetName, validRequest.AfterSheetName, validRequest.Position, null, ex);
        }
    }

    public async Task<WorksheetLayoutMutationResult> SetWorksheetVisibilityAsync(
        string workbookPath,
        WorksheetVisibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = ValidateWorksheetVisibilityRequest(request, out var validationError);
        if (validationError is not null)
        {
            return new WorksheetLayoutMutationResult(false, workbookPath, request.SheetName, "set_visibility", Visibility: NormalizeOptional(request.Visibility), Error: validationError);
        }
        var validRequest = normalizedRequest!;

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new WorksheetLayoutMutationResult(false, workbookPath, validRequest.SheetName, "set_visibility", Visibility: validRequest.Visibility, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetWorksheetVisibilityAsync(validRequest, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "worksheet_visibility_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName,
                ["visibility"] = validRequest.Visibility
            });
            return new WorksheetLayoutMutationResult(true, workbookPath, validRequest.SheetName, "set_visibility", Visibility: validRequest.Visibility);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "worksheet_visibility_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = validRequest.SheetName,
                ["visibility"] = validRequest.Visibility
            }, ex);
            return BuildWorksheetLayoutMutationError(workbookPath, validRequest.SheetName, "set_visibility", null, null, null, null, validRequest.Visibility, ex);
        }
    }

    public async Task<RefreshResult> RefreshQueryAsync(string workbookPath, string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new RefreshOptions();
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RefreshResult(false, queryName, "blocked", TimeSpan.Zero, safetyError);
        }

        if (options.Silent)
        {
            await using var _ = await _session.BeginScopeAsync(QuietSessionOptions, cancellationToken);
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var result = await workbook.RefreshQueryAsync(queryName, options, cancellationToken);
            if (result.Succeeded)
            {
                await workbook.SaveAsync(cancellationToken);
            }

            _logger.LogInfo(nameof(WorkbookService), "query_refreshed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = queryName,
                ["succeeded"] = result.Succeeded,
                ["mode"] = result.Mode
            });
            return result;
        }

        await using (var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken))
        {
            var result = await workbook.RefreshQueryAsync(queryName, options, cancellationToken);
            if (result.Succeeded)
            {
                await workbook.SaveAsync(cancellationToken);
            }

            _logger.LogInfo(nameof(WorkbookService), "query_refreshed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = queryName,
                ["succeeded"] = result.Succeeded,
                ["mode"] = result.Mode
            });
            return result;
        }
    }

    public async Task<ProbeResult> TryRunQueryAsync(string workbookPath, string queryName, string tempPrefix, CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.DiagnosticTempWrite, cancellationToken);
        if (safetyError is not null)
        {
            return new ProbeResult(false, queryName, tempPrefix, null, safetyError);
        }

        await using var _ = await _session.BeginScopeAsync(QuietSessionOptions, cancellationToken);
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var tempName = $"{tempPrefix}_{queryName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var result = await workbook.RunQueryProbeAsync(new QueryProbeRequest(queryName, tempName), cancellationToken);
        _logger.LogInfo(nameof(WorkbookService), "query_probe_ran", new Dictionary<string, object?>
        {
            ["workbookPath"] = workbookPath,
            ["queryName"] = queryName,
            ["tempPrefix"] = tempPrefix,
            ["succeeded"] = result.Succeeded
        });
        return result;
    }

    public async Task<CleanupResult> CleanupTempQueriesAsync(string workbookPath, string pattern, CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.DiagnosticTempWrite, cancellationToken);
        if (safetyError is not null)
        {
            return new CleanupResult(
                DeletedCount: 0,
                DeletedNames: Array.Empty<string>(),
                FailedNames: Array.Empty<string>(),
                Errors: new[] { safetyError });
        }

        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var result = await workbook.CleanupTempQueriesAsync(pattern, cancellationToken);
        if (result.DeletedCount > 0)
        {
            await workbook.SaveAsync(cancellationToken);
        }

        _logger.LogInfo(nameof(WorkbookService), "temp_queries_cleaned", new Dictionary<string, object?>
        {
            ["workbookPath"] = workbookPath,
            ["pattern"] = pattern,
            ["deletedCount"] = result.DeletedCount,
            ["failedCount"] = result.FailedNames?.Count ?? 0
        });
        return result;
    }

    public async Task<QueryFormulaUpdateResult> SetQueryFormulaAsync(
        string workbookPath,
        string queryName,
        string formula,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new QueryFormulaUpdateResult(false, workbookPath, queryName, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetQueryFormulaAsync(queryName, formula, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "query_formula_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = queryName
            });
            return new QueryFormulaUpdateResult(true, workbookPath, queryName);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "query_formula_set_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["queryName"] = queryName
            }, ex);
            return new QueryFormulaUpdateResult(
                false,
                workbookPath,
                queryName,
                new OperationError(
                    Code: "query_formula_update_failed",
                    Message: $"Failed to set formula for query '{queryName}'.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeReadResult> ReadRangeAsync(
        string workbookPath,
        string sheetName,
        string address,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var range = await workbook.ReadRangeAsync(address, sheetName, cancellationToken);
        return new RangeReadResult(
            range.SheetName,
            range.Address,
            ConvertValues(range.Values));
    }

    public async Task<RangeFormulaReadResult> ReadRangeFormulasAsync(
        string workbookPath,
        string sheetName,
        string address,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var range = await workbook.ReadRangeFormulasAsync(address, sheetName, cancellationToken);
        return new RangeFormulaReadResult(
            range.SheetName,
            range.Address,
            ConvertStringValues(range.Values));
    }

    public async Task<RangeFormatReadResult> ReadRangeFormatAsync(
        string workbookPath,
        string sheetName,
        string address,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var range = await workbook.ReadRangeFormatAsync(address, sheetName, cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "range_format_read", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = range.SheetName,
                ["address"] = range.Address,
                ["mixedPropertyCount"] = range.MixedProperties.Count
            });
            return new RangeFormatReadResult(true, range.SheetName, range.Address, range.Format, range.MixedProperties);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_format_read_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["sheetName"] = sheetName,
                ["address"] = address
            }, ex);
            return new RangeFormatReadResult(
                false,
                NormalizeOptional(sheetName) ?? string.Empty,
                NormalizeOptional(address) ?? string.Empty,
                new RangeFormatSnapshot(),
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_format_read_failed",
                    Message: $"Failed to read formatting for range '{sheetName}!{address}'.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeReadResult> ReadNamedRangeAsync(
        string workbookPath,
        string name,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var range = await workbook.ReadNamedRangeAsync(name, sheetName, cancellationToken);
        return new RangeReadResult(
            range.SheetName,
            range.Address,
            ConvertValues(range.Values));
    }

    public async Task<RecalculationResult> RecalculateAsync(
        string workbookPath,
        CalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = ValidateScopedTarget(request.Scope, request.SheetName, request.Address, "recalculation", out var validationError);
        if (validationError is not null)
        {
            return new RecalculationResult(
                false,
                workbookPath,
                NormalizeScopeOrDefault(request.Scope),
                NormalizeOptional(request.SheetName),
                NormalizeOptional(request.Address),
                TimeSpan.Zero,
                validationError);
        }

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RecalculationResult(false, workbookPath, target!.Scope, target.SheetName, target.Address, TimeSpan.Zero, safetyError);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInfo(nameof(WorkbookService), "recalculation_started", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["scope"] = target!.Scope,
                ["sheetName"] = target.SheetName,
                ["address"] = target.Address
            });

            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.RecalculateAsync(new CalculationRequest(target.Scope, target.SheetName, target.Address), cancellationToken);
            stopwatch.Stop();

            _logger.LogInfo(nameof(WorkbookService), "recalculation_completed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["scope"] = target.Scope,
                ["sheetName"] = target.SheetName,
                ["address"] = target.Address,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
            });

            return new RecalculationResult(true, workbookPath, target.Scope, target.SheetName, target.Address, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogInfo(nameof(WorkbookService), "recalculation_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["scope"] = target!.Scope,
                ["sheetName"] = target.SheetName,
                ["address"] = target.Address
            }, ex);
            return BuildRecalculationError(workbookPath, target.Scope, target.SheetName, target.Address, stopwatch.Elapsed, ex);
        }
    }

    public async Task<ErrorInspectionResult> InspectErrorsAsync(
        string workbookPath,
        ErrorInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = ValidateScopedTarget(request.Scope, request.SheetName, request.Address, "error_inspection", out var validationError);
        if (validationError is not null)
        {
            return new ErrorInspectionResult(
                false,
                workbookPath,
                NormalizeScopeOrDefault(request.Scope),
                NormalizeOptional(request.SheetName),
                NormalizeOptional(request.Address),
                0,
                Array.Empty<ErrorInspectionHit>(),
                validationError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var hits = await workbook.InspectErrorsAsync(new ErrorInspectionRequest(target!.Scope, target.SheetName, target.Address), cancellationToken);

            _logger.LogInfo(nameof(WorkbookService), "error_inspection_completed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["scope"] = target.Scope,
                ["sheetName"] = target.SheetName,
                ["address"] = target.Address,
                ["hitCount"] = hits.Count
            });

            return new ErrorInspectionResult(true, workbookPath, target.Scope, target.SheetName, target.Address, hits.Count, hits);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "error_inspection_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["scope"] = target!.Scope,
                ["sheetName"] = target.SheetName,
                ["address"] = target.Address
            }, ex);
            return BuildErrorInspectionError(workbookPath, target.Scope, target.SheetName, target.Address, ex);
        }
    }

    public async Task<TableReadResult> ReadTableAsync(
        string workbookPath,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ReadTableAsync(tableName, cancellationToken);
    }

    public async Task<TableDetailResult> GetTableAsync(
        string workbookPath,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetTableAsync(tableName, cancellationToken);
    }

    public async Task<TableMutationResult> CreateTableAsync(
        string workbookPath,
        TableCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, request.TableName, "create", request.SheetName, request.Address, HasHeaders: request.HasHeaders, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CreateTableAsync(request, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "table_created", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName,
                ["sheetName"] = request.SheetName
            });
            return new TableMutationResult(true, workbookPath, request.TableName, "create", request.SheetName, request.Address, HasHeaders: request.HasHeaders);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "table_create_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName
            }, ex);
            return BuildTableMutationError(workbookPath, request.TableName, "create", request.SheetName, request.Address, null, request.HasHeaders, null, ex);
        }
    }

    public async Task<TableMutationResult> ResizeTableAsync(
        string workbookPath,
        TableResizeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, request.TableName, "resize", request.SheetName, request.Address, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.ResizeTableAsync(request, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            return new TableMutationResult(true, workbookPath, request.TableName, "resize", request.SheetName, request.Address);
        }
        catch (Exception ex)
        {
            return BuildTableMutationError(workbookPath, request.TableName, "resize", request.SheetName, request.Address, null, null, null, ex);
        }
    }

    public async Task<TableMutationResult> AppendTableRowsAsync(
        string workbookPath,
        TableRowsWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, request.TableName, "append_rows", RowCount: GetRowCount(request.Values), Error: safetyError);
        }

        try
        {
            ValidateValues(request.Values, request.TableName);
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await ValidateTableWriteShapeAsync(workbook, request, cancellationToken);
            await workbook.AppendTableRowsAsync(request, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "table_rows_appended", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName,
                ["rowCount"] = GetRowCount(request.Values)
            });
            return new TableMutationResult(true, workbookPath, request.TableName, "append_rows", RowCount: GetRowCount(request.Values));
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "table_append_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName
            }, ex);
            return BuildTableMutationError(workbookPath, request.TableName, "append_rows", null, null, GetRowCount(request.Values), null, null, ex);
        }
    }

    public async Task<TableMutationResult> ReplaceTableRowsAsync(
        string workbookPath,
        TableRowsWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, request.TableName, "replace_rows", RowCount: GetRowCount(request.Values), Error: safetyError);
        }

        try
        {
            ValidateValues(request.Values, request.TableName);
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await ValidateTableWriteShapeAsync(workbook, request, cancellationToken);
            await workbook.ReplaceTableRowsAsync(request, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "table_rows_replaced", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName,
                ["rowCount"] = GetRowCount(request.Values)
            });
            return new TableMutationResult(true, workbookPath, request.TableName, "replace_rows", RowCount: GetRowCount(request.Values));
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "table_replace_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName
            }, ex);
            return BuildTableMutationError(workbookPath, request.TableName, "replace_rows", null, null, GetRowCount(request.Values), null, null, ex);
        }
    }

    public async Task<TableMutationResult> SetTableOptionsAsync(
        string workbookPath,
        TableOptionsUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, request.TableName, "set_options", HasHeaders: request.HasHeaders, ShowTotals: request.ShowTotals, Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetTableOptionsAsync(request, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "table_options_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName,
                ["hasHeaders"] = request.HasHeaders,
                ["showTotals"] = request.ShowTotals
            });
            return new TableMutationResult(true, workbookPath, request.TableName, "set_options", HasHeaders: request.HasHeaders, ShowTotals: request.ShowTotals);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "table_options_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = request.TableName
            }, ex);
            return BuildTableMutationError(workbookPath, request.TableName, "set_options", null, null, null, request.HasHeaders, request.ShowTotals, ex);
        }
    }

    public async Task<TableMutationResult> DeleteTableAsync(
        string workbookPath,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new TableMutationResult(false, workbookPath, tableName, "delete", Error: safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var table = await workbook.GetTableAsync(tableName, cancellationToken);
            await workbook.DeleteTableAsync(tableName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "table_deleted", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = tableName,
                ["sheetName"] = table.SheetName
            });
            return new TableMutationResult(true, workbookPath, tableName, "delete", table.SheetName, table.Address, table.RowCount, table.HasHeaders, table.HasTotalsRow);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "table_delete_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["tableName"] = tableName
            }, ex);
            return BuildTableMutationError(workbookPath, tableName, "delete", null, null, null, null, null, ex);
        }
    }

    public async Task<RangeWriteResult> WriteRangesAsync(
        string workbookPath,
        RangeWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeWriteResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            foreach (var write in request.Writes)
            {
                await PreflightWriteAsync(workbook, write, cancellationToken);
            }

            var appliedWrites = new List<string>(request.Writes.Count);
            foreach (var write in request.Writes)
            {
                await workbook.WriteRangeAsync(write.Address, write.Values, write.SheetName, cancellationToken);
                appliedWrites.Add(write.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "ranges_written", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = appliedWrites.Count
            });
            return new RangeWriteResult(true, workbookPath, appliedWrites.Count, appliedWrites);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_write_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = request.Writes.Count
            }, ex);
            return new RangeWriteResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
            new OperationError(
                Code: "range_write_failed",
                Message: "Failed to write one or more workbook ranges.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeFormulaWriteResult> WriteRangeFormulasAsync(
        string workbookPath,
        RangeFormulaWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeFormulaWriteResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            foreach (var write in request.Writes)
            {
                await PreflightFormulaWriteAsync(workbook, write, cancellationToken);
            }

            var appliedWrites = new List<string>(request.Writes.Count);
            foreach (var write in request.Writes)
            {
                await workbook.WriteRangeFormulasAsync(write.Address, write.Formulas, write.SheetName, cancellationToken);
                appliedWrites.Add(write.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "range_formulas_written", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = appliedWrites.Count
            });
            return new RangeFormulaWriteResult(true, workbookPath, appliedWrites.Count, appliedWrites);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_formula_write_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = request.Writes.Count
            }, ex);
            return new RangeFormulaWriteResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_formula_write_failed",
                    Message: "Failed to write one or more workbook formula ranges.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeFormatWriteResult> WriteRangeFormatsAsync(
        string workbookPath,
        RangeFormatWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeFormatWriteResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var appliedWrites = new List<string>(request.Writes.Count);
            foreach (var write in request.Writes)
            {
                ValidateFormatPatch(write.Format, write.Identifier);
                await workbook.WriteRangeFormatAsync(write.Address, write.Format, write.SheetName, cancellationToken);
                appliedWrites.Add(write.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "range_formats_set", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = appliedWrites.Count
            });
            return new RangeFormatWriteResult(true, workbookPath, appliedWrites.Count, appliedWrites);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_format_write_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["writeCount"] = request.Writes.Count
            }, ex);
            return new RangeFormatWriteResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_format_write_failed",
                    Message: "Failed to set formatting for one or more workbook ranges.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeAutofitResult> AutofitRangesAsync(
        string workbookPath,
        RangeAutofitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeAutofitResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var appliedTargets = new List<string>(request.Targets.Count);
            foreach (var target in request.Targets)
            {
                ValidateAutofitDimension(target.Dimension, target.Identifier);
                await workbook.AutofitRangeAsync(target.Address, NormalizeAutofitDimension(target.Dimension)!, target.SheetName, cancellationToken);
                appliedTargets.Add(target.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "range_autofit_completed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["targetCount"] = appliedTargets.Count
            });
            return new RangeAutofitResult(true, workbookPath, appliedTargets.Count, appliedTargets);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_autofit_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["targetCount"] = request.Targets.Count
            }, ex);
            return new RangeAutofitResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_autofit_failed",
                    Message: "Failed to autofit one or more workbook ranges.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeClearResult> ClearRangesAsync(
        string workbookPath,
        RangeClearRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeClearResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var appliedClears = new List<string>(request.Clears.Count);
            foreach (var clear in request.Clears)
            {
                await workbook.ClearRangeContentsAsync(clear.Address, clear.SheetName, cancellationToken);
                appliedClears.Add(clear.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            _logger.LogInfo(nameof(WorkbookService), "range_contents_cleared", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["clearCount"] = appliedClears.Count
            });
            return new RangeClearResult(true, workbookPath, appliedClears.Count, appliedClears);
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookService), "range_clear_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["clearCount"] = request.Clears.Count
            }, ex);
            return new RangeClearResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_clear_failed",
                    Message: "Failed to clear one or more workbook ranges.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    private static async Task PreflightWriteAsync(
        IWorkbookHandle workbook,
        RangeWriteTarget write,
        CancellationToken cancellationToken)
    {
        ValidateValues(write.Values, write.Identifier);
        var existing = await workbook.ReadRangeAsync(write.Address, write.SheetName, cancellationToken);
        var expectedRows = GetRowCount(write.Values);
        var expectedColumns = GetColumnCount(write.Values);
        var actualRows = GetRowCount(existing.Values);
        var actualColumns = GetColumnCount(existing.Values);

        if (expectedRows != actualRows || expectedColumns != actualColumns)
        {
            throw new InvalidOperationException(
                $"Write target '{write.Identifier}' has shape {actualRows}x{actualColumns}, but provided values have shape {expectedRows}x{expectedColumns}.");
        }
    }

    private static async Task PreflightFormulaWriteAsync(
        IWorkbookHandle workbook,
        RangeFormulaWriteTarget write,
        CancellationToken cancellationToken)
    {
        ValidateFormulas(write.Formulas, write.Identifier);
        var existing = await workbook.ReadRangeAsync(write.Address, write.SheetName, cancellationToken);
        var expectedRows = GetRowCount(write.Formulas);
        var expectedColumns = GetColumnCount(write.Formulas);
        var actualRows = GetRowCount(existing.Values);
        var actualColumns = GetColumnCount(existing.Values);

        if (expectedRows != actualRows || expectedColumns != actualColumns)
        {
            throw new InvalidOperationException(
                $"Formula target '{write.Identifier}' has shape {actualRows}x{actualColumns}, but provided formulas have shape {expectedRows}x{expectedColumns}.");
        }
    }

    private static void ValidateValues(object?[,] values, string identifier)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException($"Write target '{identifier}' requires at least one value.");
        }
    }

    private static void ValidateFormulas(string?[,] formulas, string identifier)
    {
        if (formulas.Length == 0)
        {
            throw new InvalidOperationException($"Formula target '{identifier}' requires at least one formula.");
        }

        for (var row = formulas.GetLowerBound(0); row <= formulas.GetUpperBound(0); row++)
        {
            for (var column = formulas.GetLowerBound(1); column <= formulas.GetUpperBound(1); column++)
            {
                var formula = formulas[row, column];
                if (string.IsNullOrWhiteSpace(formula))
                {
                    throw new InvalidOperationException($"Formula target '{identifier}' does not allow null or blank formulas.");
                }
            }
        }
    }

    private static void ValidateFormatPatch(RangeFormatPatch format, string identifier)
    {
        if (format.IsEmpty)
        {
            throw new InvalidOperationException($"Format target '{identifier}' requires at least one formatting property.");
        }

        if (format.HasFill is false && format.FillColor is not null)
        {
            throw new InvalidOperationException($"Format target '{identifier}' cannot set 'fillColor' while requesting no-fill state.");
        }
    }

    private static void ValidateAutofitDimension(string dimension, string identifier)
    {
        if (NormalizeAutofitDimension(dimension) is null)
        {
            throw new InvalidOperationException($"Autofit target '{identifier}' must use dimension 'rows', 'columns', or 'both'.");
        }
    }

    private static string? NormalizeAutofitDimension(string? dimension)
    {
        var normalized = NormalizeOptional(dimension)?.ToLowerInvariant();
        return normalized is "rows" or "columns" or "both"
            ? normalized
            : null;
    }

    private static WorksheetMoveRequest? ValidateWorksheetMoveRequest(WorksheetMoveRequest request, out OperationError? error)
    {
        var normalized = ValidatePlacement(
            request.SheetName,
            request.BeforeSheetName,
            request.AfterSheetName,
            request.Position,
            allowNoPlacement: false,
            out error);

        return normalized is null
            ? null
            : new WorksheetMoveRequest(normalized.SheetName, normalized.BeforeSheetName, normalized.AfterSheetName, normalized.Position);
    }

    private static WorksheetCopyRequest? ValidateWorksheetCopyRequest(WorksheetCopyRequest request, out OperationError? error)
    {
        var normalizedSheetName = NormalizeOptional(request.SheetName);
        var normalizedNewSheetName = NormalizeOptional(request.NewSheetName);
        if (normalizedSheetName is null || normalizedNewSheetName is null)
        {
            error = new OperationError(
                Code: "worksheet_copy_invalid",
                Message: "Worksheet copy requires source and destination sheet names.",
                Detail: "Provide both 'sheetName' and 'newSheetName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        var normalizedPlacement = ValidatePlacement(
            normalizedSheetName,
            request.BeforeSheetName,
            request.AfterSheetName,
            request.Position,
            allowNoPlacement: true,
            out error);

        if (error is not null || normalizedPlacement is null)
        {
            return null;
        }

        return new WorksheetCopyRequest(
            normalizedSheetName,
            normalizedNewSheetName,
            normalizedPlacement.BeforeSheetName,
            normalizedPlacement.AfterSheetName ?? normalizedSheetName,
            normalizedPlacement.Position);
    }

    private static WorksheetVisibilityRequest? ValidateWorksheetVisibilityRequest(WorksheetVisibilityRequest request, out OperationError? error)
    {
        var sheetName = NormalizeOptional(request.SheetName);
        var visibility = NormalizeVisibility(request.Visibility);

        if (sheetName is null || visibility is null)
        {
            error = new OperationError(
                Code: "worksheet_set_visibility_invalid",
                Message: "Worksheet visibility change requires a valid sheet and visibility.",
                Detail: "Use visibility 'visible', 'hidden', or 'veryHidden'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new WorksheetVisibilityRequest(sheetName, visibility);
    }

    private static QueryCreateRequest? ValidateQueryCreateRequest(QueryCreateRequest request, out OperationError? error)
    {
        var queryName = NormalizeOptional(request.QueryName);
        var formula = NormalizeOptional(request.Formula);
        var loadMode = NormalizeQueryLoadMode(request.LoadMode);
        var destinationSheetName = NormalizeOptional(request.DestinationSheetName);
        var destinationAddress = NormalizeOptional(request.DestinationAddress);

        if (queryName is null || formula is null || loadMode is null)
        {
            error = new OperationError(
                Code: "query_create_invalid",
                Message: "Query create requires a name, formula, and supported load mode.",
                Detail: "Use load mode 'none', 'worksheet', 'dataModel', or 'worksheetAndDataModel'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (LoadModeRequiresWorksheetTarget(loadMode) &&
            (destinationSheetName is null || destinationAddress is null))
        {
            error = new OperationError(
                Code: "query_create_invalid",
                Message: "Worksheet-loaded queries require a destination sheet and address.",
                Detail: "Provide both 'destinationSheetName' and 'destinationAddress' when loadMode targets a worksheet.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new QueryCreateRequest(queryName, formula, loadMode, destinationSheetName, destinationAddress);
    }

    private static QueryRenameRequest? ValidateQueryRenameRequest(QueryRenameRequest request, out OperationError? error)
    {
        var queryName = NormalizeOptional(request.QueryName);
        var newQueryName = NormalizeOptional(request.NewQueryName);
        if (queryName is null || newQueryName is null)
        {
            error = new OperationError(
                Code: "query_rename_invalid",
                Message: "Query rename requires source and destination query names.",
                Detail: "Provide both 'queryName' and 'newQueryName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new QueryRenameRequest(queryName, newQueryName);
    }

    private static ConnectionRenameRequest? ValidateConnectionRenameRequest(ConnectionRenameRequest request, out OperationError? error)
    {
        var connectionName = NormalizeOptional(request.ConnectionName);
        var newConnectionName = NormalizeOptional(request.NewConnectionName);
        if (connectionName is null || newConnectionName is null)
        {
            error = new OperationError(
                Code: "connection_rename_invalid",
                Message: "Connection rename requires source and destination connection names.",
                Detail: "Provide both 'connectionName' and 'newConnectionName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new ConnectionRenameRequest(connectionName, newConnectionName);
    }

    private static ConnectionUpdateRequest? ValidateConnectionUpdateRequest(ConnectionUpdateRequest request, out OperationError? error)
    {
        var connectionName = NormalizeOptional(request.ConnectionName);
        if (connectionName is null)
        {
            error = new OperationError(
                Code: "connection_update_invalid",
                Message: "Connection update requires a connection name.",
                Detail: "Provide 'connectionName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (request.RefreshWithRefreshAll is null &&
            request.BackgroundQuery is null &&
            request.EnableRefresh is null &&
            request.RefreshOnFileOpen is null &&
            request.SavePassword is null)
        {
            error = new OperationError(
                Code: "connection_update_invalid",
                Message: "Connection update requires at least one mutable field.",
                Detail: "Provide one or more of 'refreshWithRefreshAll', 'backgroundQuery', 'enableRefresh', 'refreshOnFileOpen', or 'savePassword'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return request with { ConnectionName = connectionName };
    }

    private static WorkbookVisibilityRequest? ValidateWorkbookVisibilityRequest(WorkbookVisibilityRequest request, out OperationError? error)
    {
        var visibility = NormalizeWorkbookVisibility(request.Visibility);
        if (visibility is null)
        {
            error = new OperationError(
                Code: "workbook_set_visibility_invalid",
                Message: "Workbook visibility change requires a supported visibility value.",
                Detail: "Use visibility 'visible' or 'hidden'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new WorkbookVisibilityRequest(visibility);
    }

    private static WorkbookProtectionUpdateRequest? ValidateWorkbookProtectionUpdateRequest(WorkbookProtectionUpdateRequest request, out OperationError? error)
    {
        var mode = NormalizeWorkbookProtectionMode(request.Mode);
        var password = NormalizeOptional(request.Password);
        if (mode is null)
        {
            error = new OperationError(
                Code: "workbook_set_protection_invalid",
                Message: "Workbook protection requires a supported mode.",
                Detail: "Use mode 'protect' or 'unprotect'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (mode == WorkbookProtectionModes.Unprotect &&
            (request.ProtectStructure is not null || request.ProtectWindows is not null))
        {
            error = new OperationError(
                Code: "workbook_set_protection_invalid",
                Message: "Workbook unprotect does not accept protect flags.",
                Detail: "Remove 'protectStructure' and 'protectWindows' when mode is 'unprotect'.",
                Source: nameof(WorkbookService));
            return null;
        }

        bool? protectStructure = mode == WorkbookProtectionModes.Protect ? request.ProtectStructure ?? true : null;
        bool? protectWindows = mode == WorkbookProtectionModes.Protect ? request.ProtectWindows ?? false : null;

        error = null;
        return new WorkbookProtectionUpdateRequest(mode, password, protectStructure, protectWindows);
    }

    private static string? NormalizeVisibility(string? visibility)
    {
        var normalized = NormalizeOptional(visibility);
        if (normalized is null)
        {
            return null;
        }

        return normalized.ToLowerInvariant() switch
        {
            "visible" => "visible",
            "hidden" => "hidden",
            "veryhidden" => "veryHidden",
            _ => null
        };
    }

    private static string? NormalizeWorkbookVisibility(string? visibility)
    {
        var normalized = NormalizeOptional(visibility);
        if (normalized is null)
        {
            return null;
        }

        return normalized.ToLowerInvariant() switch
        {
            "visible" => WorkbookVisibilityModes.Visible,
            "hidden" => WorkbookVisibilityModes.Hidden,
            _ => null
        };
    }

    private static string? NormalizeQueryLoadMode(string? loadMode)
    {
        var normalized = NormalizeOptional(loadMode);
        if (normalized is null)
        {
            return QueryLoadModes.None;
        }

        return normalized.ToLowerInvariant() switch
        {
            "none" => QueryLoadModes.None,
            "worksheet" => QueryLoadModes.Worksheet,
            "datamodel" => QueryLoadModes.DataModel,
            "worksheetanddatamodel" => QueryLoadModes.WorksheetAndDataModel,
            _ => null
        };
    }

    private static string? NormalizeWorkbookProtectionMode(string? mode)
    {
        var normalized = NormalizeOptional(mode);
        if (normalized is null)
        {
            return null;
        }

        return normalized.ToLowerInvariant() switch
        {
            "protect" => WorkbookProtectionModes.Protect,
            "unprotect" => WorkbookProtectionModes.Unprotect,
            _ => null
        };
    }

    private static bool LoadModeRequiresWorksheetTarget(string loadMode) =>
        string.Equals(loadMode, QueryLoadModes.Worksheet, StringComparison.Ordinal) ||
        string.Equals(loadMode, QueryLoadModes.WorksheetAndDataModel, StringComparison.Ordinal);

    private static WorksheetPlacement? ValidatePlacement(
        string? sheetName,
        string? beforeSheetName,
        string? afterSheetName,
        string? position,
        bool allowNoPlacement,
        out OperationError? error)
    {
        var normalizedSheetName = NormalizeOptional(sheetName);
        var normalizedBeforeSheetName = NormalizeOptional(beforeSheetName);
        var normalizedAfterSheetName = NormalizeOptional(afterSheetName);
        var normalizedPosition = NormalizeOptional(position)?.ToLowerInvariant();

        if (normalizedSheetName is null)
        {
            error = new OperationError(
                Code: "worksheet_layout_invalid",
                Message: "Worksheet layout operation requires a sheet name.",
                Detail: "Provide a non-empty 'sheetName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (normalizedPosition is not null && normalizedPosition is not "first" and not "last")
        {
            error = new OperationError(
                Code: "worksheet_layout_invalid",
                Message: "Worksheet placement must use a supported position value.",
                Detail: "Use 'first' or 'last' for the 'position' selector.",
                Source: nameof(WorkbookService));
            return null;
        }

        var selectorCount = 0;
        if (normalizedBeforeSheetName is not null)
        {
            selectorCount++;
        }

        if (normalizedAfterSheetName is not null)
        {
            selectorCount++;
        }

        if (normalizedPosition is not null)
        {
            selectorCount++;
        }

        if (selectorCount == 0 && !allowNoPlacement)
        {
            error = new OperationError(
                Code: "worksheet_layout_invalid",
                Message: "Worksheet placement requires exactly one selector.",
                Detail: "Provide one of 'beforeSheetName', 'afterSheetName', or 'position'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (selectorCount > 1)
        {
            error = new OperationError(
                Code: "worksheet_layout_invalid",
                Message: "Worksheet placement is ambiguous.",
                Detail: "Provide only one of 'beforeSheetName', 'afterSheetName', or 'position'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return new WorksheetPlacement(normalizedSheetName, normalizedBeforeSheetName, normalizedAfterSheetName, normalizedPosition);
    }

    private static async Task ValidateTableWriteShapeAsync(
        IWorkbookHandle workbook,
        TableRowsWriteRequest request,
        CancellationToken cancellationToken)
    {
        var table = await workbook.GetTableAsync(request.TableName, cancellationToken);
        var expectedColumns = table.ColumnCount;
        var actualColumns = GetColumnCount(request.Values);
        if (actualColumns != expectedColumns)
        {
            throw new InvalidOperationException(
                $"Table '{request.TableName}' has {expectedColumns} columns, but provided rows have {actualColumns} columns.");
        }
    }

    private static int GetRowCount(Array values) =>
        values.GetLength(0);

    private static int GetColumnCount(Array values) =>
        values.GetLength(1);

    private static IReadOnlyList<IReadOnlyList<object?>> ConvertValues(object?[,] values)
    {
        var rows = new List<IReadOnlyList<object?>>();
        for (var row = values.GetLowerBound(0); row <= values.GetUpperBound(0); row++)
        {
            var columns = new List<object?>();
            for (var column = values.GetLowerBound(1); column <= values.GetUpperBound(1); column++)
            {
                columns.Add(values[row, column]);
            }

            rows.Add(columns);
        }

        return rows;
    }

    private static IReadOnlyList<IReadOnlyList<string?>> ConvertStringValues(object?[,] values)
    {
        var rows = new List<IReadOnlyList<string?>>();
        for (var row = values.GetLowerBound(0); row <= values.GetUpperBound(0); row++)
        {
            var columns = new List<string?>();
            for (var column = values.GetLowerBound(1); column <= values.GetUpperBound(1); column++)
            {
                columns.Add(values[row, column]?.ToString());
            }

            rows.Add(columns);
        }

        return rows;
    }

    private static ScopedTarget? ValidateScopedTarget(
        string scope,
        string? sheetName,
        string? address,
        string operation,
        out OperationError? error)
    {
        var normalizedScope = NormalizeScope(scope);
        var normalizedSheetName = NormalizeOptional(sheetName);
        var normalizedAddress = NormalizeOptional(address);

        if (normalizedScope is null)
        {
            error = new OperationError(
                Code: $"{operation}_target_invalid",
                Message: $"Invalid target scope for {operation.Replace('_', ' ')}.",
                Detail: "Use scope 'workbook', 'worksheet', or 'range'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (normalizedScope == "worksheet" && normalizedSheetName is null)
        {
            error = new OperationError(
                Code: $"{operation}_target_invalid",
                Message: $"Invalid target scope for {operation.Replace('_', ' ')}.",
                Detail: "A worksheet-scoped target requires 'sheetName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (normalizedScope == "range" && normalizedSheetName is null)
        {
            error = new OperationError(
                Code: $"{operation}_target_invalid",
                Message: $"Invalid target scope for {operation.Replace('_', ' ')}.",
                Detail: "A range-scoped target requires 'sheetName'.",
                Source: nameof(WorkbookService));
            return null;
        }

        if (normalizedScope == "range" && normalizedAddress is null)
        {
            error = new OperationError(
                Code: $"{operation}_target_invalid",
                Message: $"Invalid target scope for {operation.Replace('_', ' ')}.",
                Detail: "A range-scoped target requires 'address'.",
                Source: nameof(WorkbookService));
            return null;
        }

        error = null;
        return normalizedScope switch
        {
            "workbook" => new ScopedTarget(normalizedScope, null, null),
            "worksheet" => new ScopedTarget(normalizedScope, normalizedSheetName, null),
            _ => new ScopedTarget(normalizedScope, normalizedSheetName, normalizedAddress)
        };
    }

    private static string NormalizeScopeOrDefault(string? scope) =>
        NormalizeScope(scope) ?? NormalizeOptional(scope) ?? string.Empty;

    private static string? NormalizeScope(string? scope)
    {
        var normalized = NormalizeOptional(scope)?.ToLowerInvariant();
        return normalized is "workbook" or "worksheet" or "range"
            ? normalized
            : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetScope(string? sheetName) =>
        string.IsNullOrWhiteSpace(sheetName) ? "Workbook" : "Worksheet";

    private static NameMutationResult BuildNameMutationError(
        string workbookPath,
        string name,
        string action,
        string? sheetName,
        string? refersTo,
        Exception ex) =>
        new(
            false,
            workbookPath,
            name,
            action,
            GetScope(sheetName),
            sheetName,
            refersTo,
            new OperationError(
                Code: $"name_{action}_failed",
                Message: $"Failed to {action} name '{name}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static QueryMutationResult BuildQueryMutationError(
        string workbookPath,
        string queryName,
        string action,
        string? newQueryName,
        string? loadMode,
        string? destinationSheetName,
        string? destinationAddress,
        string? connectionName,
        Exception ex) =>
        new(
            false,
            workbookPath,
            queryName,
            action,
            newQueryName,
            loadMode,
            destinationSheetName,
            destinationAddress,
            connectionName,
            new OperationError(
                Code: $"query_{action}_failed",
                Message: $"Failed to {action.Replace('_', ' ')} query '{queryName}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static ConnectionMutationResult BuildConnectionMutationError(
        string workbookPath,
        string connectionName,
        string action,
        string? newConnectionName,
        ConnectionUpdateRequest? request,
        Exception ex) =>
        new(
            false,
            workbookPath,
            connectionName,
            action,
            newConnectionName,
            request?.RefreshWithRefreshAll,
            request?.BackgroundQuery,
            request?.EnableRefresh,
            request?.RefreshOnFileOpen,
            request?.SavePassword,
            new OperationError(
                Code: $"connection_{action}_failed",
                Message: $"Failed to {action.Replace('_', ' ')} connection '{connectionName}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static RecalculationResult BuildRecalculationError(
        string workbookPath,
        string scope,
        string? sheetName,
        string? address,
        TimeSpan duration,
        Exception ex) =>
        new(
            false,
            workbookPath,
            scope,
            sheetName,
            address,
            duration,
            new OperationError(
                Code: "recalculation_failed",
                Message: "Failed to recalculate the targeted workbook scope.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static ErrorInspectionResult BuildErrorInspectionError(
        string workbookPath,
        string scope,
        string? sheetName,
        string? address,
        Exception ex) =>
        new(
            false,
            workbookPath,
            scope,
            sheetName,
            address,
            0,
            Array.Empty<ErrorInspectionHit>(),
            new OperationError(
                Code: "error_inspection_failed",
                Message: "Failed to inspect workbook error state for the targeted scope.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static WorksheetMutationResult BuildWorksheetMutationError(
        string workbookPath,
        string sheetName,
        string action,
        string? newSheetName,
        Exception ex) =>
        new(
            false,
            workbookPath,
            sheetName,
            action,
            newSheetName,
            new OperationError(
                Code: $"worksheet_{action}_failed",
                Message: $"Failed to {action} worksheet '{sheetName}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static WorksheetLayoutMutationResult BuildWorksheetLayoutMutationError(
        string workbookPath,
        string sheetName,
        string action,
        string? newSheetName,
        string? beforeSheetName,
        string? afterSheetName,
        string? position,
        string? visibility,
        Exception ex) =>
        new(
            false,
            workbookPath,
            sheetName,
            action,
            newSheetName,
            beforeSheetName,
            afterSheetName,
            position,
            visibility,
            new OperationError(
                Code: $"worksheet_{action}_failed",
                Message: $"Failed to {action.Replace('_', ' ')} worksheet '{sheetName}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static WorkbookSaveResult BuildWorkbookSaveError(
        string sourceWorkbookPath,
        string workbookPath,
        string operation,
        string? connectionId,
        Exception ex) =>
        new(
            false,
            sourceWorkbookPath,
            workbookPath,
            operation,
            connectionId,
            new OperationError(
                Code: $"workbook_{operation}_failed",
                Message: $"Failed to {operation.Replace('_', ' ')} workbook '{sourceWorkbookPath}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static TableMutationResult BuildTableMutationError(
        string workbookPath,
        string tableName,
        string action,
        string? sheetName,
        string? address,
        int? rowCount,
        bool? hasHeaders,
        bool? showTotals,
        Exception ex) =>
        new(
            false,
            workbookPath,
            tableName,
            action,
            sheetName,
            address,
            rowCount,
            hasHeaders,
            showTotals,
            new OperationError(
                Code: $"table_{action}_failed",
                Message: $"Failed to {action.Replace('_', ' ')} for table '{tableName}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static WorkbookStructureMutationResult BuildWorkbookStructureMutationError(
        string workbookPath,
        string action,
        string? visibility,
        string? mode,
        bool? protectStructure,
        bool? protectWindows,
        Exception ex) =>
        new(
            false,
            workbookPath,
            action,
            visibility,
            mode,
            protectStructure,
            protectWindows,
            new OperationError(
                Code: $"workbook_{action}_failed",
                Message: $"Failed to {action.Replace('_', ' ')} for workbook '{workbookPath}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));

    private static WorkbookStructureMutationResult BuildWorkbookProtectionError(
        string workbookPath,
        WorkbookProtectionUpdateRequest request,
        Exception ex)
    {
        var detail = ex.Message;
        var lower = detail.ToLowerInvariant();
        var code = "workbook_set_protection_failed";
        var message = $"Failed to {request.Mode} workbook protection.";

        if (request.Mode == WorkbookProtectionModes.Unprotect)
        {
            if (lower.Contains("password"))
            {
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    code = "workbook_protection_password_required";
                    message = "Workbook protection password is required to unprotect this workbook.";
                }
                else
                {
                    code = "workbook_protection_invalid_password";
                    message = "Workbook protection password was rejected.";
                }
            }
        }

        return new WorkbookStructureMutationResult(
            false,
            workbookPath,
            "set_protection",
            Mode: request.Mode,
            ProtectStructure: request.ProtectStructure,
            ProtectWindows: request.ProtectWindows,
            Error: new OperationError(
                Code: code,
                Message: message,
                Detail: detail,
                Source: nameof(WorkbookService)));
    }

    private sealed record ScopedTarget(string Scope, string? SheetName, string? Address);
    private sealed record WorksheetPlacement(string SheetName, string? BeforeSheetName, string? AfterSheetName, string? Position);
}
