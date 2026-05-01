using ExcelMcp.Core;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.ComAdapter.Interop;

internal static class StaOperationRunner
{
    public static T Run<T>(
        Func<T> operation,
        TimeSpan timeout,
        string timeoutCode,
        string timeoutMessage,
        string timeoutDetail,
        IGridPilotLogger? logger = null)
        => Run(operation, timeout, timeoutCode, timeoutMessage, () => timeoutDetail, logger);

    public static T Run<T>(
        Func<T> operation,
        TimeSpan timeout,
        string timeoutCode,
        string timeoutMessage,
        Func<string> timeoutDetailFactory,
        IGridPilotLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(timeoutDetailFactory);

        var resolvedLogger = logger ?? GridPilotNullLogger.Instance;
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(operation());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "GridPilot STA operation"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!completion.Task.Wait(timeout))
        {
            resolvedLogger.LogInfo(nameof(StaOperationRunner), "sta_operation_timeout", new Dictionary<string, object?>
            {
                ["timeoutMs"] = timeout.TotalMilliseconds,
                ["threadApartmentState"] = thread.GetApartmentState().ToString()
            });

            throw new ExcelSessionTargetException(timeoutCode, timeoutMessage, timeoutDetailFactory());
        }

        return completion.Task.GetAwaiter().GetResult();
    }
}
