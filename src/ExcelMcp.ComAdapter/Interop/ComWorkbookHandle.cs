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
    private bool _closed;

    public ComWorkbookHandle(object workbook)
    {
        _workbook = workbook;
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

        return null;
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
            if (connection is null)
            {
                return false;
            }

            queryName = NormalizeQueryName(ComDispatch.GetProperty<string>(connection, "Name"));
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
}
