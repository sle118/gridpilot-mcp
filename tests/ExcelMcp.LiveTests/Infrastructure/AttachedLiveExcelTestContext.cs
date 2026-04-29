using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter;
using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using System.Runtime.Versioning;
namespace ExcelMcp.LiveTests.Infrastructure;

[SupportedOSPlatform("windows")]
internal sealed class AttachedLiveExcelTestContext : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _resources = [];
    private readonly ExcelApplicationSession _ownerSession;
    private readonly IWorkbookHandle _ownerWorkbook;

    private AttachedLiveExcelTestContext(
        string workbookPath,
        ExcelApplicationSession ownerSession,
        IWorkbookHandle ownerWorkbook,
        ExcelApplicationSession attachedSession)
    {
        WorkbookPath = workbookPath;
        _ownerSession = ownerSession;
        _ownerWorkbook = ownerWorkbook;
        Session = attachedSession;
        WorkbookService = new WorkbookService(attachedSession);
    }

    public string WorkbookPath { get; }

    public ExcelApplicationSession Session { get; }

    public WorkbookService WorkbookService { get; }

    public static async Task<AttachedLiveExcelTestContext> CreateAsync()
    {
        var workbookPath = LiveExcelEnvironment.CreateTempWorkbookCopy();
        var ownerSession = ExcelApplicationSession.CreateNew(visible: false);
        var ownerWorkbook = await ownerSession.OpenWorkbookAsync(workbookPath);
        ExcelApplicationSession attachedSession;
        try
        {
            attachedSession = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.ForWorkbook(workbookPath));
        }
        catch (ExcelSessionTargetException ex)
        {
            await ownerWorkbook.DisposeAsync();
            await ownerSession.DisposeAsync();
            DeleteTempWorkbookWithRetry(workbookPath);
            throw Xunit.Sdk.SkipException.ForSkip($"Attached-session live tests require a usable workbook-targeted Excel attachment: {ex.Message}");
        }

        return new AttachedLiveExcelTestContext(workbookPath, ownerSession, ownerWorkbook, attachedSession);
    }

    public async ValueTask DisposeAsync()
    {
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            await _resources[index].DisposeAsync();
        }

        _resources.Clear();
        await Session.DisposeAsync();
        await _ownerWorkbook.DisposeAsync();
        await _ownerSession.DisposeAsync();
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
