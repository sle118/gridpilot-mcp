namespace ExcelMcp.LiveTests.Infrastructure;

internal sealed class AttachedLiveExcelFactAttribute : FactAttribute
{
    public AttachedLiveExcelFactAttribute()
    {
        var availability = LiveExcelEnvironment.GetAvailability();
        if (!availability.IsAvailable)
        {
            Skip = availability.Reason;
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_ATTACHED_LIVE_EXCEL_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_ATTACHED_LIVE_EXCEL_TESTS=1 to enable attached-session live Excel tests.";
        }
    }
}
