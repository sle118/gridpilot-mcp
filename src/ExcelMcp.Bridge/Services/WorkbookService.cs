using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;

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
            ["failedCount"] = result.FailedNames.Count
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
}
