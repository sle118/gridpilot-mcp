using ExcelMcp.ComAdapter;
using ExcelMcp.UnitTests.Fakes;

namespace ExcelMcp.UnitTests.Services;

public sealed class ExcelApplicationSessionTests
{
    private static readonly SessionState DefaultState = new(
        DisplayAlerts: true,
        ScreenUpdating: true,
        EnableEvents: true,
        Visible: true,
        FastCombine: null);

    [Fact]
    public async Task BeginScopeAsync_RestoresStateAfterDispose()
    {
        var application = new FakeExcelApplicationHandle(DefaultState);
        await using var session = new ExcelApplicationSession(application);

        await using (await session.BeginScopeAsync(new SessionOptions(
            DisplayAlerts: false,
            ScreenUpdating: false,
            EnableEvents: false)))
        {
            Assert.False(application.CurrentState.DisplayAlerts);
            Assert.False(application.CurrentState.ScreenUpdating);
            Assert.False(application.CurrentState.EnableEvents);
        }

        Assert.Equal(DefaultState, application.CurrentState);
        Assert.Single(application.RestoreHistory);
    }

    [Fact]
    public async Task BeginScopeAsync_RestoresStateAfterException()
    {
        var application = new FakeExcelApplicationHandle(DefaultState);
        await using var session = new ExcelApplicationSession(application);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var scope = await session.BeginScopeAsync(new SessionOptions(
                DisplayAlerts: false,
                ScreenUpdating: false,
                EnableEvents: false));

            throw new InvalidOperationException("boom");
        });

        Assert.Equal(DefaultState, application.CurrentState);
        Assert.Single(application.RestoreHistory);
    }

    [Fact]
    public async Task BeginScopeAsync_SupportsNestedLifoRestore()
    {
        var application = new FakeExcelApplicationHandle(DefaultState);
        await using var session = new ExcelApplicationSession(application);

        await using var outer = await session.BeginScopeAsync(new SessionOptions(
            DisplayAlerts: false,
            ScreenUpdating: false));
        var outerState = application.CurrentState;

        await using (await session.BeginScopeAsync(new SessionOptions(
            EnableEvents: false)))
        {
            Assert.False(application.CurrentState.DisplayAlerts);
            Assert.False(application.CurrentState.ScreenUpdating);
            Assert.False(application.CurrentState.EnableEvents);
        }

        Assert.Equal(outerState, application.CurrentState);

        await outer.DisposeAsync();

        Assert.Equal(DefaultState, application.CurrentState);
        Assert.Equal(2, application.RestoreHistory.Count);
    }
}
