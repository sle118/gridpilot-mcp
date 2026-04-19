using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using System.Runtime.Versioning;

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

    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => throw NotYetImplemented();

    private static NotSupportedException NotYetImplemented() =>
        new("Workbook/query operations are not implemented in the session foundation slice.");
}
