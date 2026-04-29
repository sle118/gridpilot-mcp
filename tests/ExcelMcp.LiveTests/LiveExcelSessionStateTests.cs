using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveExcelSessionStateTests
{
    [LiveExcelFact]
    public async Task BeginScopeAsync_RestoresApplicationStateAfterDispose()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        var initialState = await context.Session.GetStateAsync();

        await using (await context.Session.BeginScopeAsync(new SessionOptions(
            DisplayAlerts: false,
            ScreenUpdating: false,
            EnableEvents: false)))
        {
            var scopedState = await context.Session.GetStateAsync();
            Assert.False(scopedState.DisplayAlerts);
            Assert.False(scopedState.ScreenUpdating);
            Assert.False(scopedState.EnableEvents);
        }

        var restoredState = await context.Session.GetStateAsync();
        Assert.Equal(initialState.DisplayAlerts, restoredState.DisplayAlerts);
        Assert.Equal(initialState.ScreenUpdating, restoredState.ScreenUpdating);
        Assert.Equal(initialState.EnableEvents, restoredState.EnableEvents);
    }

    [LiveExcelFact]
    public async Task BeginScopeAsync_RestoresApplicationStateAfterException()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        var initialState = await context.Session.GetStateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var _ = await context.Session.BeginScopeAsync(new SessionOptions(
                DisplayAlerts: false,
                ScreenUpdating: false,
                EnableEvents: false));

            var scopedState = await context.Session.GetStateAsync();
            Assert.False(scopedState.DisplayAlerts);
            Assert.False(scopedState.ScreenUpdating);
            Assert.False(scopedState.EnableEvents);

            throw new InvalidOperationException("intentional live test failure");
        });

        var restoredState = await context.Session.GetStateAsync();
        Assert.Equal(initialState.DisplayAlerts, restoredState.DisplayAlerts);
        Assert.Equal(initialState.ScreenUpdating, restoredState.ScreenUpdating);
        Assert.Equal(initialState.EnableEvents, restoredState.EnableEvents);
    }
}
