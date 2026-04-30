using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using System.Collections;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal sealed class ComWorkbookHandle : IWorkbookHandle
{
    private readonly object _workbook;
    private readonly bool _closeOnDispose;
    private bool _closed;

    public ComWorkbookHandle(object workbook, bool closeOnDispose = true)
    {
        _workbook = workbook;
        _closeOnDispose = closeOnDispose;
    }

    public string Name => ComDispatch.GetProperty<string>(_workbook, "Name");

    public string FullPath => ComDispatch.GetProperty<string>(_workbook, "FullName");

    public ValueTask DisposeAsync()
    {
        if (_closed)
        {
            ComDispatch.ReleaseIfComObject(_workbook);
            return ValueTask.CompletedTask;
        }

        if (!_closeOnDispose)
        {
            _closed = true;
            ComDispatch.ReleaseIfComObject(_workbook);
            return ValueTask.CompletedTask;
        }

        return new ValueTask(CloseAsync(saveChanges: false));
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ComDispatch.InvokeMethod(_workbook, "Save");
        return Task.CompletedTask;
    }

    public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_closed)
        {
            return Task.CompletedTask;
        }

        try
        {
            ComDispatch.InvokeMethod(_workbook, "Close", saveChanges);
            _closed = true;
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(_workbook);
        }
    }

    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sheets = GetCollection(_workbook, "Sheets");
        try
        {
            var summaries = new List<SheetSummary>();
            foreach (var sheet in ComDispatch.Enumerate(sheets))
            {
                try
                {
                    summaries.Add(new SheetSummary(
                        Name: GetStringProperty(sheet, "Name"),
                        Kind: GetOptionalProperty(sheet, "Type")?.ToString() ?? "Worksheet",
                        Visible: IsVisible(GetOptionalProperty(sheet, "Visible"))));
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(sheet);
                }
            }

            return Task.FromResult<IReadOnlyList<SheetSummary>>(summaries);
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(sheets);
        }
    }

    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TableSummary>>(EnumerateTables().ToArray());
    }

    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var worksheetLoads = EnumerateTables()
            .Where(table => table.QueryName is not null)
            .Select(table => table.QueryName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dataModelLoads = EnumerateConnectionSummaries()
            .Where(connection => IsDataModelConnectionType(connection.Type))
            .Select(connection => NormalizeQueryName(connection.Name))
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queries = GetCollection(_workbook, "Queries");
        try
        {
            var summaries = new List<QuerySummary>();
            foreach (var query in ComDispatch.Enumerate(queries))
            {
                try
                {
                    var name = GetStringProperty(query, "Name");
                    summaries.Add(new QuerySummary(
                        Name: name,
                        LoadToWorksheet: worksheetLoads.Contains(name),
                        LoadToDataModel: dataModelLoads.Contains(name),
                        Formula: GetOptionalProperty(query, "Formula")?.ToString()));
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(query);
                }
            }

            return Task.FromResult<IReadOnlyList<QuerySummary>>(summaries);
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(queries);
        }
    }

    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ConnectionSummary>>(EnumerateConnectionSummaries().ToArray());
    }

    public Task<IReadOnlyList<NameSummary>> ListNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summaries = new List<NameSummary>();
        foreach (var entry in EnumerateNames())
        {
            try
            {
                summaries.Add(BuildNameSummary(entry.NameObject));
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(entry.NameObject);
            }
        }

        return Task.FromResult<IReadOnlyList<NameSummary>>(summaries);
    }

    private IEnumerable<ConnectionSummary> EnumerateConnectionSummaries()
    {
        var connections = GetCollection(_workbook, "Connections");
        try
        {
            foreach (var connection in ComDispatch.Enumerate(connections))
            {
                try
                {
                    yield return new ConnectionSummary(
                        Name: GetStringProperty(connection, "Name"),
                        Type: GetOptionalProperty(connection, "Type")?.ToString() ?? "Unknown",
                        RefreshWithRefreshAll: ToBoolean(GetOptionalProperty(connection, "RefreshWithRefreshAll")));
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(connection);
                }
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(connections);
        }
    }

    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = FindQueryByName(queryName)
            ?? throw new InvalidOperationException($"Query '{queryName}' was not found.");

        try
        {
            return Task.FromResult(new QueryDefinition(
                Name: GetStringProperty(query, "Name"),
                Formula: GetOptionalProperty(query, "Formula")?.ToString() ?? string.Empty,
                Description: GetOptionalProperty(query, "Description")?.ToString()));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(query);
        }
    }

    public Task<NameSummary> GetNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nameObject = FindNameByName(name, sheetName)
            ?? throw new InvalidOperationException($"Name '{name}' was not found.");

        try
        {
            return Task.FromResult(BuildNameSummary(nameObject));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(nameObject);
        }
    }

    public Task CreateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (FindNameByName(name, sheetName) is { } existing)
        {
            ComDispatch.ReleaseIfComObject(existing);
            throw new InvalidOperationException($"Name '{BuildDisplayName(name, sheetName)}' already exists.");
        }

        object? target = null;
        object? names = null;
        object? created = null;
        try
        {
            target = string.IsNullOrWhiteSpace(sheetName) ? _workbook : GetWorksheet(sheetName);
            names = GetCollection(target, "Names");
            created = ComDispatch.InvokeMethod(names, "Add", name, refersTo)
                ?? throw new InvalidOperationException($"Excel did not create name '{BuildDisplayName(name, sheetName)}'.");
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(created);
            ComDispatch.ReleaseIfComObject(names);
            if (!ReferenceEquals(target, _workbook))
            {
                ComDispatch.ReleaseIfComObject(target);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nameObject = FindNameByName(name, sheetName)
            ?? throw new InvalidOperationException($"Name '{BuildDisplayName(name, sheetName)}' was not found.");

        try
        {
            ComDispatch.SetProperty(nameObject, "RefersTo", refersTo);
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(nameObject);
        }
    }

    public Task DeleteNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nameObject = FindNameByName(name, sheetName)
            ?? throw new InvalidOperationException($"Name '{BuildDisplayName(name, sheetName)}' was not found.");

        try
        {
            ComDispatch.InvokeMethod(nameObject, "Delete");
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(nameObject);
        }
    }
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingQuery = FindQueryByName(queryName);
        if (existingQuery is not null)
        {
            try
            {
                ComDispatch.SetProperty(existingQuery, "Formula", formula);
                return Task.CompletedTask;
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(existingQuery);
            }
        }

        var queries = GetCollection(_workbook, "Queries");
        try
        {
            var created = ComDispatch.InvokeMethod(queries, "Add", queryName, formula);
            ComDispatch.ReleaseIfComObject(created);
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(queries);
        }
    }
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new RefreshOptions();

        var stopwatch = Stopwatch.StartNew();
        object? queryTable = null;
        object? connection = null;

        try
        {
            if (options.PreferSynchronousTableRefresh)
            {
                queryTable = FindQueryTableByQueryName(queryName);
                if (queryTable is not null)
                {
                    ComDispatch.InvokeMethod(queryTable, "Refresh", false);
                    WaitForAsyncQueries(options.Timeout, cancellationToken, stopwatch);
                    return Task.FromResult(BuildRefreshResult(true, queryName, "query-table", stopwatch.Elapsed));
                }
            }

            connection = FindConnectionByQueryName(queryName);
            if (connection is null)
            {
                return Task.FromResult(BuildRefreshResult(
                    false,
                    queryName,
                    "query",
                    stopwatch.Elapsed,
                    new OperationError(
                        Code: "query_not_found",
                        Message: $"Query '{queryName}' was not found for targeted refresh.",
                        Source: nameof(ComWorkbookHandle))));
            }

            ComDispatch.InvokeMethod(connection, "Refresh");
            WaitForAsyncQueries(options.Timeout, cancellationToken, stopwatch);
            return Task.FromResult(BuildRefreshResult(true, queryName, "connection", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BuildRefreshResult(
                false,
                queryName,
                queryTable is not null ? "query-table" : "connection",
                stopwatch.Elapsed,
                new OperationError(
                    Code: "query_refresh_failed",
                    Message: $"Failed to refresh query '{queryName}'.",
                    Detail: ex.Message,
                    Source: nameof(ComWorkbookHandle))));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(queryTable);
            ComDispatch.ReleaseIfComObject(connection);
        }
    }
    public async Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var originalConnectionCounts = SnapshotConnectionNameCounts();
        var tempSheetName = BuildTempSheetName(request.TempQueryName);

        try
        {
            await SetQueryFormulaAsync(request.TempQueryName, BuildProbeFormula(request), cancellationToken);

            object? worksheet = null;
            object? listObject = null;
            try
            {
                worksheet = CreateWorksheet(tempSheetName);
                listObject = LoadQueryPreviewTable(worksheet, request.TempQueryName);
                var rangeData = ReadListObjectRange(listObject, tempSheetName);

                return new ProbeResult(
                    Succeeded: true,
                    TargetQuery: request.TargetQueryName,
                    TempQuery: request.TempQueryName,
                    Preview: rangeData);
            }
            catch (Exception ex)
            {
                return new ProbeResult(
                    Succeeded: false,
                    TargetQuery: request.TargetQueryName,
                    TempQuery: request.TempQueryName,
                    Preview: null,
                    Error: new OperationError(
                        Code: "query_probe_failed",
                        Message: $"Failed to probe query '{request.TargetQueryName}'.",
                        Detail: ex.Message,
                        Source: nameof(ComWorkbookHandle)));
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(listObject);
                if (request.CleanupAfterRun)
                {
                    DeleteWorksheetIfExists(tempSheetName);
                }

                ComDispatch.ReleaseIfComObject(worksheet);
            }
        }
        finally
        {
            if (request.CleanupAfterRun)
            {
                await CleanupTempProbeArtifactsAsync(request.TempQueryName, originalConnectionCounts, cancellationToken);
            }
        }
    }
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deletedNames = new List<string>();
        var failedNames = new List<string>();
        var errors = new List<OperationError>();

        var matcher = BuildQueryMatcher(prefixOrPattern);
        var matches = EnumerateQueries()
            .Where(query => matcher(query.Name))
            .ToArray();

        foreach (var match in matches)
        {
            try
            {
                if (!ComDispatch.TryInvokeMethod(match.Query, "Delete", out _))
                {
                    throw new InvalidOperationException($"Query '{match.Name}' does not expose a Delete method.");
                }

                deletedNames.Add(match.Name);
            }
            catch (Exception ex)
            {
                failedNames.Add(match.Name);
                errors.Add(new OperationError(
                    Code: "query_cleanup_failed",
                    Message: $"Failed to delete query '{match.Name}'.",
                    Detail: ex.Message,
                    Source: nameof(ComWorkbookHandle)));
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(match.Query);
            }
        }

        return Task.FromResult(new CleanupResult(
            DeletedCount: deletedNames.Count,
            DeletedNames: deletedNames,
            FailedNames: failedNames,
            Errors: errors));
    }

    public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        try
        {
            table = FindTableByName(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' was not found.");

            return Task.FromResult(BuildTableReadResult(table, tableName));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task<TableDetailResult> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        try
        {
            table = FindTableByName(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' was not found.");

            return Task.FromResult(BuildTableDetailResult(table, tableName));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task CreateTableAsync(TableCreateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (FindTableByName(request.TableName) is { } existing)
        {
            ComDispatch.ReleaseIfComObject(existing);
            throw new InvalidOperationException($"Table '{request.TableName}' already exists.");
        }

        object? worksheet = null;
        object? range = null;
        object? listObjects = null;
        object? table = null;
        try
        {
            worksheet = GetWorksheet(request.SheetName);
            range = ComDispatch.GetProperty<object>(worksheet, "Range", request.Address);
            listObjects = GetCollection(worksheet, "ListObjects");
            table = ComDispatch.InvokeMethod(listObjects, "Add", 1, range, Type.Missing, request.HasHeaders ? 1 : 2)
                ?? throw new InvalidOperationException($"Excel did not create table '{request.TableName}'.");
            ComDispatch.SetProperty(table, "Name", request.TableName);
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(table);
            ComDispatch.ReleaseIfComObject(listObjects);
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    public Task ResizeTableAsync(TableResizeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        object? worksheet = null;
        object? range = null;
        try
        {
            table = FindTableByName(request.TableName)
                ?? throw new InvalidOperationException($"Table '{request.TableName}' was not found.");
            worksheet = GetWorksheet(request.SheetName);
            range = ComDispatch.GetProperty<object>(worksheet, "Range", request.Address);
            ComDispatch.InvokeMethod(table, "Resize", range);
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(worksheet);
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task AppendTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        object? listRows = null;
        try
        {
            table = FindTableByName(request.TableName)
                ?? throw new InvalidOperationException($"Table '{request.TableName}' was not found.");
            listRows = GetCollection(table, "ListRows");

            for (var rowIndex = 0; rowIndex < request.Values.GetLength(0); rowIndex++)
            {
                object? listRow = null;
                object? rowRange = null;
                try
                {
                    listRow = ComDispatch.InvokeMethod(listRows, "Add")
                        ?? throw new InvalidOperationException($"Excel did not append a row to table '{request.TableName}'.");
                    rowRange = ComDispatch.GetProperty<object>(listRow, "Range");
                    ComDispatch.SetProperty(rowRange, "Value2", ExtractRowMatrix(request.Values, rowIndex));
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(rowRange);
                    ComDispatch.ReleaseIfComObject(listRow);
                }
            }

            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(listRows);
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task ReplaceTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        object? listRows = null;
        object? bodyRange = null;
        try
        {
            table = FindTableByName(request.TableName)
                ?? throw new InvalidOperationException($"Table '{request.TableName}' was not found.");
            listRows = GetCollection(table, "ListRows");

            var desiredCount = request.Values.GetLength(0);
            var currentCount = ComDispatch.GetProperty<int>(listRows, "Count");

            while (currentCount > desiredCount)
            {
                object? row = null;
                try
                {
                    row = ComDispatch.GetProperty<object>(listRows, "Item", currentCount)
                        ?? throw new InvalidOperationException($"Excel did not expose row {currentCount} for table '{request.TableName}'.");
                    ComDispatch.InvokeMethod(row, "Delete");
                    currentCount--;
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(row);
                }
            }

            while (currentCount < desiredCount)
            {
                object? row = null;
                try
                {
                    row = ComDispatch.InvokeMethod(listRows, "Add")
                        ?? throw new InvalidOperationException($"Excel did not append a row to table '{request.TableName}'.");
                    currentCount++;
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(row);
                }
            }

            bodyRange = GetOptionalProperty(table, "DataBodyRange");
            if (bodyRange is null)
            {
                throw new InvalidOperationException($"Table '{request.TableName}' does not expose a writable data body.");
            }

            ComDispatch.SetProperty(bodyRange, "Value2", request.Values);
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(bodyRange);
            ComDispatch.ReleaseIfComObject(listRows);
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task SetTableOptionsAsync(TableOptionsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? table = null;
        try
        {
            table = FindTableByName(request.TableName)
                ?? throw new InvalidOperationException($"Table '{request.TableName}' was not found.");

            if (request.HasHeaders.HasValue)
            {
                ComDispatch.SetProperty(table, "ShowHeaders", request.HasHeaders.Value);
            }

            if (request.ShowTotals.HasValue)
            {
                ComDispatch.SetProperty(table, "ShowTotals", request.ShowTotals.Value);
            }

            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(table);
        }
    }

    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? worksheet = null;
        object? range = null;

        try
        {
            worksheet = GetWorksheet(sheetName);
            range = ComDispatch.GetProperty<object>(worksheet, "Range", address);
            var values = ComDispatch.GetProperty<object?>(range!, "Value2");
            return Task.FromResult(new RangeData(
                SheetName: ComDispatch.GetProperty<string>(worksheet, "Name"),
                Address: GetOptionalProperty(range!, "Address")?.ToString() ?? address,
                Values: ConvertToMatrix(values)));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    public Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? nameObject = null;
        object? range = null;
        try
        {
            nameObject = FindNameByName(name, sheetName)
                ?? throw new InvalidOperationException($"Name '{name}' was not found.");
            range = ComDispatch.GetProperty<object>(nameObject, "RefersToRange");
            return Task.FromResult(ReadRangeData(range, GetStringProperty(nameObject, "Name")));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(nameObject);
        }
    }
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? worksheet = null;
        object? range = null;

        try
        {
            worksheet = GetWorksheet(sheetName);
            range = ComDispatch.GetProperty<object>(worksheet, "Range", address);
            if (GetElementCount(values) == 1)
            {
                ComDispatch.SetProperty(range!, "Value2", FirstValue(values));
            }
            else
            {
                ComDispatch.SetProperty(range!, "Value2", values);
            }
            return Task.CompletedTask;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    private RefreshResult BuildRefreshResult(bool succeeded, string queryName, string mode, TimeSpan duration, OperationError? error = null) =>
        new(succeeded, queryName, mode, duration, error);

    private void WaitForAsyncQueries(TimeSpan? timeout, CancellationToken cancellationToken, Stopwatch stopwatch)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? application = null;
        try
        {
            application = ComDispatch.GetProperty<object>(_workbook, "Application");
            ComDispatch.InvokeMethod(application, "CalculateUntilAsyncQueriesDone");
        }
        finally
        {
            // Do not final-release the Application RCW obtained through the workbook.
            // The owning Excel session keeps the application object alive for the duration
            // of the operation and must remain valid for later state restoration.
        }

        if (timeout is { } maxDuration && stopwatch.Elapsed > maxDuration)
        {
            throw new TimeoutException($"Excel query work exceeded the requested timeout of {maxDuration}.");
        }
    }

    private object? FindConnectionByQueryName(string queryName)
    {
        var targetConnectionName = $"Query - {queryName}";
        var connections = GetCollection(_workbook, "Connections");
        try
        {
            foreach (var connection in ComDispatch.Enumerate(connections))
            {
                if (string.Equals(GetStringProperty(connection, "Name"), targetConnectionName, StringComparison.OrdinalIgnoreCase))
                {
                    return connection;
                }

                ComDispatch.ReleaseIfComObject(connection);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(connections);
        }

        var queryTable = FindQueryTableByQueryName(queryName);
        if (queryTable is null)
        {
            return null;
        }

        try
        {
            var connection = ComDispatch.GetProperty<object>(queryTable, "WorkbookConnection");
            return connection;
        }
        catch
        {
            return null;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(queryTable);
        }

    }

    private object? FindQueryTableByQueryName(string queryName)
    {
        var sheets = GetCollection(_workbook, "Sheets");
        try
        {
            foreach (var sheet in ComDispatch.Enumerate(sheets))
            {
                object? listObjects = null;
                try
                {
                    if (!ComDispatch.TryGetProperty(sheet, "ListObjects", out listObjects) || listObjects is null)
                    {
                        continue;
                    }

                    foreach (var table in ComDispatch.Enumerate(listObjects))
                    {
                        object? queryTable = null;
                        try
                        {
                            if (TryGetQueryName(table, out var candidateQueryName) &&
                                string.Equals(candidateQueryName, queryName, StringComparison.OrdinalIgnoreCase))
                            {
                                queryTable = ComDispatch.GetProperty<object>(table, "QueryTable");
                                return queryTable;
                            }
                        }
                        finally
                        {
                            ComDispatch.ReleaseIfComObject(table);
                        }
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(listObjects);
                    ComDispatch.ReleaseIfComObject(sheet);
                }
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(sheets);
        }

        return null;
    }

    private string BuildProbeFormula(QueryProbeRequest request)
    {
        var escapedName = request.TargetQueryName.Replace("\"", "\"\"");
        return $"""
let
    Source = #"{escapedName}",
    Preview = Table.FirstN(Source, {request.MaxRows})
in
    Preview
""";
    }

    private object CreateWorksheet(string sheetName)
    {
        var worksheets = GetCollection(_workbook, "Worksheets");
        try
        {
            var worksheet = ComDispatch.InvokeMethod(worksheets, "Add")
                ?? throw new InvalidOperationException("Excel did not return a worksheet when adding a probe sheet.");
            ComDispatch.SetProperty(worksheet, "Name", sheetName);
            return worksheet;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(worksheets);
        }
    }

    private object LoadQueryPreviewTable(object worksheet, string queryName)
    {
        var listObjects = ComDispatch.GetProperty<object>(worksheet, "ListObjects");
        object? destination = null;
        try
        {
            destination = ComDispatch.GetProperty<object>(worksheet, "Range", "A1");
            var connectionString = BuildMashupConnectionString(queryName);
            var listObject = ComDispatch.InvokeMethod(listObjects, "Add", 0, connectionString, true, 1, destination)
                ?? throw new InvalidOperationException($"Excel did not create a probe table for '{queryName}'.");

            object? queryTable = null;
            try
            {
                queryTable = ComDispatch.GetProperty<object>(listObject, "QueryTable");
                ComDispatch.SetProperty(queryTable, "CommandType", 2);
                ComDispatch.SetProperty(queryTable, "CommandText", new[] { $"SELECT * FROM [{queryName}]" });
                ComDispatch.InvokeMethod(queryTable, "Refresh", false);
                return listObject;
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(queryTable);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(destination);
            ComDispatch.ReleaseIfComObject(listObjects);
        }
    }

    private RangeData ReadListObjectRange(object listObject, string fallbackSheetName)
    {
        object? range = null;
        object? worksheet = null;
        try
        {
            range = ComDispatch.GetProperty<object>(listObject, "Range");
            worksheet = ComDispatch.GetProperty<object>(range, "Worksheet");
            var values = ComDispatch.GetProperty<object?>(range, "Value2");
            return new RangeData(
                SheetName: GetOptionalProperty(worksheet, "Name")?.ToString() ?? fallbackSheetName,
                Address: GetOptionalProperty(range, "Address")?.ToString() ?? "$A$1",
                Values: ConvertToMatrix(values));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(worksheet);
            ComDispatch.ReleaseIfComObject(range);
        }
    }

    private async Task CleanupTempProbeArtifactsAsync(string tempQueryName, Dictionary<string, int> originalConnectionCounts, CancellationToken cancellationToken)
    {
        await CleanupTempQueriesAsync(tempQueryName, cancellationToken);
        DeleteExtraConnections(originalConnectionCounts);
    }

    private void DeleteWorksheetIfExists(string sheetName)
    {
        object? worksheet = null;
        try
        {
            worksheet = FindWorksheetByName(sheetName);
            if (worksheet is not null)
            {
                ComDispatch.InvokeMethod(worksheet, "Delete");
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    private void DeleteExtraConnections(Dictionary<string, int> originalConnectionCounts)
    {
        var connections = GetCollection(_workbook, "Connections");
        try
        {
            var currentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var connection in ComDispatch.Enumerate(connections))
            {
                try
                {
                    var name = GetStringProperty(connection, "Name");
                    currentCounts.TryGetValue(name, out var seenCount);
                    seenCount++;
                    currentCounts[name] = seenCount;

                    originalConnectionCounts.TryGetValue(name, out var originalCount);
                    if (seenCount > originalCount)
                    {
                        ComDispatch.InvokeMethod(connection, "Delete");
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(connection);
                }
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(connections);
        }
    }

    private Dictionary<string, int> SnapshotConnectionNameCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in EnumerateConnectionSummaries())
        {
            counts.TryGetValue(connection.Name, out var current);
            counts[connection.Name] = current + 1;
        }

        return counts;
    }

    private object GetWorksheet(string? sheetName)
    {
        if (!string.IsNullOrWhiteSpace(sheetName))
        {
            var worksheet = FindWorksheetByName(sheetName);
            if (worksheet is null)
            {
                throw new InvalidOperationException($"Worksheet '{sheetName}' was not found.");
            }

            return worksheet;
        }

        return ComDispatch.GetProperty<object>(_workbook, "ActiveSheet");
    }

    private IEnumerable<(string Name, object NameObject)> EnumerateNames()
    {
        var names = GetCollection(_workbook, "Names");
        try
        {
            foreach (var nameObject in ComDispatch.Enumerate(names))
            {
                yield return (GetStringProperty(nameObject, "Name"), nameObject);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(names);
        }
    }

    private object? FindNameByName(string name, string? sheetName = null)
    {
        foreach (var candidate in EnumerateNames())
        {
            if (NameMatches(candidate.Name, name, sheetName, candidate.NameObject))
            {
                return candidate.NameObject;
            }

            ComDispatch.ReleaseIfComObject(candidate.NameObject);
        }

        return null;
    }

    private object? FindWorksheetByName(string sheetName)
    {
        var worksheets = GetCollection(_workbook, "Worksheets");
        try
        {
            foreach (var worksheet in ComDispatch.Enumerate(worksheets))
            {
                if (string.Equals(GetStringProperty(worksheet, "Name"), sheetName, StringComparison.OrdinalIgnoreCase))
                {
                    return worksheet;
                }

                ComDispatch.ReleaseIfComObject(worksheet);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(worksheets);
        }

        return null;
    }

    private static string BuildMashupConnectionString(string queryName) =>
        $"OLEDB;Provider=Microsoft.Mashup.OleDb.1;Data Source=$Workbook$;Location={queryName};Extended Properties=\"\"";

    private static string BuildTempSheetName(string tempQueryName)
    {
        const string suffix = "_sheet";
        var candidate = tempQueryName + suffix;
        return candidate.Length <= 31 ? candidate : candidate[..31];
    }

    private static object?[,] ConvertToMatrix(object? value)
    {
        if (value is object?[,] matrix)
        {
            return matrix;
        }

        var singleValue = (object?[,])Array.CreateInstance(typeof(object), [1, 1], [1, 1]);
        singleValue[1, 1] = value;
        return singleValue;
    }

    private static int GetElementCount(Array values)
    {
        var count = 1;
        for (var dimension = 0; dimension < values.Rank; dimension++)
        {
            count *= values.GetLength(dimension);
        }

        return count;
    }

    private static object? FirstValue(object?[,] values) =>
        values[values.GetLowerBound(0), values.GetLowerBound(1)];

    private IEnumerable<TableSummary> EnumerateTables()
    {
        var sheets = GetCollection(_workbook, "Sheets");
        try
        {
            foreach (var sheet in ComDispatch.Enumerate(sheets))
            {
                var sheetName = GetStringProperty(sheet, "Name");
                if (!ComDispatch.TryGetProperty(sheet, "ListObjects", out var listObjects) || listObjects is null)
                {
                    continue;
                }

                try
                {
                    foreach (var table in ComDispatch.Enumerate(listObjects))
                    {
                        try
                        {
                            yield return new TableSummary(
                                SheetName: sheetName,
                                TableName: ComDispatch.GetProperty<string>(table, "Name"),
                                Address: GetAddress(table),
                                IsQueryBacked: TryGetQueryName(table, out var queryName),
                                QueryName: queryName);
                        }
                        finally
                        {
                            ComDispatch.ReleaseIfComObject(table);
                        }
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(listObjects);
                    ComDispatch.ReleaseIfComObject(sheet);
                }
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(sheets);
        }
    }

    private object? FindTableByName(string tableName)
    {
        var sheets = GetCollection(_workbook, "Sheets");
        try
        {
            foreach (var sheet in ComDispatch.Enumerate(sheets))
            {
                object? listObjects = null;
                try
                {
                    if (!ComDispatch.TryGetProperty(sheet, "ListObjects", out listObjects) || listObjects is null)
                    {
                        continue;
                    }

                    foreach (var table in ComDispatch.Enumerate(listObjects))
                    {
                        if (string.Equals(GetStringProperty(table, "Name"), tableName, StringComparison.OrdinalIgnoreCase))
                        {
                            return table;
                        }

                        ComDispatch.ReleaseIfComObject(table);
                    }
                }
                finally
                {
                    ComDispatch.ReleaseIfComObject(listObjects);
                    ComDispatch.ReleaseIfComObject(sheet);
                }
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(sheets);
        }

        return null;
    }

    private IEnumerable<(string Name, object Query)> EnumerateQueries()
    {
        var queries = GetCollection(_workbook, "Queries");
        try
        {
            foreach (var query in ComDispatch.Enumerate(queries))
            {
                yield return (GetStringProperty(query, "Name"), query);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(queries);
        }
    }

    private object? FindQueryByName(string queryName)
    {
        foreach (var candidate in EnumerateQueries())
        {
            if (string.Equals(candidate.Name, queryName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Query;
            }

            ComDispatch.ReleaseIfComObject(candidate.Query);
        }

        return null;
    }

    private static object GetCollection(object target, string propertyName) =>
        ComDispatch.GetProperty<object>(target, propertyName);

    private static string GetStringProperty(object target, string propertyName) =>
        GetOptionalProperty(target, propertyName)?.ToString() ?? string.Empty;

    private static object? GetOptionalProperty(object target, string propertyName)
    {
        return ComDispatch.TryGetProperty(target, propertyName, out var value) ? value : null;
    }

    private static bool IsVisible(object? visibleValue)
    {
        return visibleValue switch
        {
            null => true,
            bool flag => flag,
            sbyte signedByte => signedByte != 0,
            short signedShort => signedShort != 0,
            int signedInt => signedInt != 0,
            long signedLong => signedLong != 0,
            byte unsignedByte => unsignedByte != 0,
            _ => ToBoolean(visibleValue)
        };
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool flag => flag,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => Convert.ToInt32(value) != 0
        };
    }

    private static string GetAddress(object table)
    {
        object? range = null;
        try
        {
            range = ComDispatch.GetProperty<object>(table, "Range");
            return ComDispatch.InvokeMethod(range, "Address")?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
        }
    }

    private static bool TryGetQueryName(object table, out string? queryName)
    {
        queryName = null;

        object? queryTable = null;
        object? connection = null;
        try
        {
            queryTable = ComDispatch.GetProperty<object>(table, "QueryTable");
            if (queryTable is null)
            {
                return false;
            }

            connection = ComDispatch.GetProperty<object>(queryTable, "WorkbookConnection");
            if (connection is not null)
            {
                queryName = NormalizeQueryName(ComDispatch.GetProperty<string>(connection, "Name"));
                if (!string.IsNullOrWhiteSpace(queryName) &&
                    !string.Equals(queryName, "Connection", StringComparison.OrdinalIgnoreCase) &&
                    !Regex.IsMatch(queryName, @"^Connection\d*$", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            queryName = TryGetQueryNameFromCommandText(queryTable);
            return !string.IsNullOrWhiteSpace(queryName);
        }
        catch
        {
            return false;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(connection);
            ComDispatch.ReleaseIfComObject(queryTable);
        }
    }

    private static string? TryGetQueryNameFromCommandText(object queryTable)
    {
        var commandText = GetOptionalProperty(queryTable, "CommandText");
        if (commandText is Array values && values.Length > 0)
        {
            commandText = values.GetValue(values.GetLowerBound(0));
        }

        if (commandText is not string text || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"\[(?<name>[^\]]+)\]");
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static string? NormalizeQueryName(string? connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return null;
        }

        const string prefix = "Query - ";
        return connectionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? connectionName[prefix.Length..]
            : connectionName;
    }

    private static bool IsDataModelConnectionType(string connectionType)
    {
        if (int.TryParse(connectionType, out var numericType))
        {
            return numericType == 7;
        }

        return connectionType.Contains("MODEL", StringComparison.OrdinalIgnoreCase);
    }

    private static Func<string, bool> BuildQueryMatcher(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return _ => false;
        }

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return name => regex.IsMatch(name);
        }

        return name => name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static NotSupportedException NotYetImplemented() =>
        new("This workbook operation is not implemented in the inventory slice.");

    private static RangeData ReadRangeData(object range, string fallbackSheetName)
    {
        object? worksheet = null;
        try
        {
            worksheet = ComDispatch.GetProperty<object>(range, "Worksheet");
            var values = ComDispatch.GetProperty<object?>(range, "Value2");
            return new RangeData(
                SheetName: GetOptionalProperty(worksheet, "Name")?.ToString() ?? fallbackSheetName,
                Address: GetOptionalProperty(range, "Address")?.ToString() ?? string.Empty,
                Values: ConvertToMatrix(values));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    private static NameSummary BuildNameSummary(object nameObject)
    {
        object? parent = null;
        object? range = null;
        try
        {
            parent = GetOptionalProperty(nameObject, "Parent");
            range = GetOptionalProperty(nameObject, "RefersToRange");

            var sheetName = parent is not null && IsWorksheetObject(parent)
                ? GetStringProperty(parent, "Name")
                : null;

            return new NameSummary(
                Name: GetStringProperty(nameObject, "Name"),
                Scope: sheetName is null ? "Workbook" : "Worksheet",
                SheetName: sheetName,
                RefersTo: GetOptionalProperty(nameObject, "RefersTo")?.ToString() ?? string.Empty,
                Address: range is null ? null : GetOptionalProperty(range, "Address")?.ToString());
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
            ComDispatch.ReleaseIfComObject(parent);
        }
    }

    private static TableReadResult BuildTableReadResult(object table, string fallbackName)
    {
        object? worksheet = null;
        object? headerRange = null;
        object? bodyRange = null;
        object? tableRange = null;
        try
        {
            worksheet = GetOptionalProperty(table, "Parent");
            headerRange = GetOptionalProperty(table, "HeaderRowRange");
            bodyRange = GetOptionalProperty(table, "DataBodyRange");
            tableRange = GetOptionalProperty(table, "Range");

            var headerValues = headerRange is null
                ? Array.Empty<string>()
                : FlattenHeaderValues(ComDispatch.GetProperty<object?>(headerRange, "Value2"));
            var rows = bodyRange is null
                ? Array.Empty<IReadOnlyList<object?>>()
                : ConvertValues(ConvertToMatrix(ComDispatch.GetProperty<object?>(bodyRange, "Value2")));

            return new TableReadResult(
                TableName: GetStringProperty(table, "Name") is { Length: > 0 } actualName ? actualName : fallbackName,
                SheetName: worksheet is null ? string.Empty : GetStringProperty(worksheet, "Name"),
                Address: tableRange is null ? string.Empty : GetOptionalProperty(tableRange, "Address")?.ToString() ?? string.Empty,
                Headers: headerValues,
                Rows: rows,
                HasTotalsRow: ToBoolean(GetOptionalProperty(table, "ShowTotals")));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(tableRange);
            ComDispatch.ReleaseIfComObject(bodyRange);
            ComDispatch.ReleaseIfComObject(headerRange);
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    private static TableDetailResult BuildTableDetailResult(object table, string fallbackName)
    {
        object? worksheet = null;
        object? headerRange = null;
        object? bodyRange = null;
        object? tableRange = null;
        try
        {
            worksheet = GetOptionalProperty(table, "Parent");
            headerRange = GetOptionalProperty(table, "HeaderRowRange");
            bodyRange = GetOptionalProperty(table, "DataBodyRange");
            tableRange = GetOptionalProperty(table, "Range");

            var headers = headerRange is null
                ? Array.Empty<string>()
                : FlattenHeaderValues(ComDispatch.GetProperty<object?>(headerRange, "Value2"));
            var rowCount = bodyRange is null
                ? 0
                : ConvertToMatrix(ComDispatch.GetProperty<object?>(bodyRange, "Value2")).GetLength(0);
            var columnCount = headers.Count > 0
                ? headers.Count
                : bodyRange is null
                    ? 0
                    : ConvertToMatrix(ComDispatch.GetProperty<object?>(bodyRange, "Value2")).GetLength(1);

            return new TableDetailResult(
                TableName: GetStringProperty(table, "Name") is { Length: > 0 } actualName ? actualName : fallbackName,
                SheetName: worksheet is null ? string.Empty : GetStringProperty(worksheet, "Name"),
                Address: tableRange is null ? string.Empty : GetOptionalProperty(tableRange, "Address")?.ToString() ?? string.Empty,
                Headers: headers,
                RowCount: rowCount,
                ColumnCount: columnCount,
                HasHeaders: ToBoolean(GetOptionalProperty(table, "ShowHeaders")),
                HasTotalsRow: ToBoolean(GetOptionalProperty(table, "ShowTotals")),
                IsQueryBacked: TryGetQueryName(table, out var queryName),
                QueryName: queryName);
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(tableRange);
            ComDispatch.ReleaseIfComObject(bodyRange);
            ComDispatch.ReleaseIfComObject(headerRange);
            ComDispatch.ReleaseIfComObject(worksheet);
        }
    }

    private static IReadOnlyList<string> FlattenHeaderValues(object? value)
    {
        var matrix = ConvertToMatrix(value);
        var headers = new List<string>();
        for (var column = matrix.GetLowerBound(1); column <= matrix.GetUpperBound(1); column++)
        {
            headers.Add(matrix[matrix.GetLowerBound(0), column]?.ToString() ?? string.Empty);
        }

        return headers;
    }

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

    private static object?[,] ExtractRowMatrix(object?[,] values, int rowIndex)
    {
        var columnCount = values.GetLength(1);
        var row = new object?[1, columnCount];
        for (var column = 0; column < columnCount; column++)
        {
            row[0, column] = values[rowIndex, column];
        }

        return row;
    }

    private static bool IsWorksheetObject(object value)
    {
        var typeName = value.GetType().Name;
        return typeName.Contains("Worksheet", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("_Worksheet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NameMatches(string candidateName, string requestedName, string? requestedSheetName, object nameObject)
    {
        var summary = BuildNameSummary(nameObject);
        var localName = GetLocalName(candidateName);

        if (!string.Equals(localName, requestedName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidateName, requestedName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedSheetName))
        {
            return string.Equals(summary.Scope, "Workbook", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(summary.Scope, "Worksheet", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(summary.SheetName, requestedSheetName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLocalName(string candidateName)
    {
        var bangIndex = candidateName.LastIndexOf('!');
        return bangIndex >= 0 ? candidateName[(bangIndex + 1)..] : candidateName;
    }

    private static string BuildDisplayName(string name, string? sheetName) =>
        string.IsNullOrWhiteSpace(sheetName) ? name : $"{sheetName}!{name}";
}
