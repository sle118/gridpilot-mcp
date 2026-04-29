using Xunit;

namespace ExcelMcp.LiveTests.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class LiveExcelFactAttribute : FactAttribute
{
    public LiveExcelFactAttribute()
    {
        var availability = LiveExcelEnvironment.GetAvailability();
        if (!availability.IsAvailable)
        {
            Skip = availability.Reason;
        }
    }
}
