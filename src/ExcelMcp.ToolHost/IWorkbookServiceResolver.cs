using ExcelMcp.Bridge.Services;

namespace ExcelMcp.ToolHost;

internal interface IWorkbookServiceResolver
{
    Task<T> ExecuteAsync<T>(
        string workbookPath,
        Func<WorkbookService, Task<T>> action,
        CancellationToken cancellationToken = default);
}
