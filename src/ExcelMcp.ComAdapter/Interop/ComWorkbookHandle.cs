using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using System.Collections;
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
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => throw NotYetImplemented();
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
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();

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
                                TableName: GetStringProperty(table, "Name"),
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
        var range = GetOptionalProperty(table, "Range");
        if (range is null)
        {
            return string.Empty;
        }

        try
        {
            if (ComDispatch.TryInvokeMethod(range, "Address", out var address, false, false))
            {
                return address?.ToString() ?? string.Empty;
            }

            return GetOptionalProperty(range, "Address")?.ToString() ?? string.Empty;
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(range);
        }
    }

    private static bool TryGetQueryName(object table, out string? queryName)
    {
        queryName = null;

        var queryTable = GetOptionalProperty(table, "QueryTable");
        if (queryTable is null)
        {
            return false;
        }

        try
        {
            var connection = GetOptionalProperty(queryTable, "WorkbookConnection");
            if (connection is null)
            {
                return false;
            }

            try
            {
                queryName = NormalizeQueryName(GetStringProperty(connection, "Name"));
                return !string.IsNullOrWhiteSpace(queryName);
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(connection);
            }
        }
        finally
        {
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
