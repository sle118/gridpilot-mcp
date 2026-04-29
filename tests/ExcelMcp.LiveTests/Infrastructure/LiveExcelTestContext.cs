using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter;
using ExcelMcp.Core.Abstractions;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests.Infrastructure;

[SupportedOSPlatform("windows")]
internal sealed class LiveExcelTestContext : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _resources = [];

    private LiveExcelTestContext(string workbookPath, ExcelApplicationSession session)
    {
        WorkbookPath = workbookPath;
        Session = session;
        WorkbookService = new WorkbookService(session);
    }

    public string WorkbookPath { get; }

    public ExcelApplicationSession Session { get; }

    public WorkbookService WorkbookService { get; }

    public static Task<LiveExcelTestContext> CreateAsync()
    {
        var tempWorkbookPath = LiveExcelEnvironment.CreateTempWorkbookCopy();
        var session = ExcelApplicationSession.CreateNew(visible: false);

        return Task.FromResult(new LiveExcelTestContext(tempWorkbookPath, session));
    }

    public async Task<IWorkbookHandle> OpenWorkbookAsync()
    {
        var workbook = await Session.OpenWorkbookAsync(WorkbookPath);
        _resources.Add(workbook);
        return workbook;
    }

    public async ValueTask DisposeAsync()
    {
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            await _resources[index].DisposeAsync();
        }

        _resources.Clear();
        await Session.DisposeAsync();
        DeleteTempWorkbookWithRetry(WorkbookPath);
    }

    private static void DeleteTempWorkbookWithRetry(string workbookPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(workbookPath))
                {
                    File.Delete(workbookPath);
                }

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
        }
    }
}
