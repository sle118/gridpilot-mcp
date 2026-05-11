using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class ProfileOverviewPresenterTests
{
    [Fact]
    public void Create_ReportsMissingProfileConfiguration()
    {
        var state = ProfileOverviewPresenter.Create(new TrayProfileContext(null));

        Assert.False(state.CanRunProfileActions);
        Assert.Equal("No profile configured", state.Status);
        Assert.Contains("--profile <path>", state.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ReportsInvalidProfileIssues()
    {
        using var workspace = TrayProfileTestWorkspace.Create();
        var state = ProfileOverviewPresenter.Create(new TrayProfileContext(workspace.WriteInvalidJsonProfile()));

        Assert.False(state.CanRunProfileActions);
        Assert.Equal("Profile invalid or missing", state.Status);
        Assert.Contains("invalid_json", state.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ValidProfileSummarizesEnvKeysWithoutValues()
    {
        using var workspace = TrayProfileTestWorkspace.Create();
        var profilePath = workspace.WriteProfile(
            new Dictionary<string, string?>
            {
                ["GRIDPILOT_LOG_LEVEL"] = "info",
                ["SECRET_TOKEN"] = "do-not-display"
            });

        var state = ProfileOverviewPresenter.Create(new TrayProfileContext(profilePath));

        Assert.True(state.CanRunProfileActions);
        Assert.Equal("Profile loaded", state.Status);
        Assert.Contains("Env keys: GRIDPILOT_LOG_LEVEL, SECRET_TOKEN", state.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-display", state.Details, StringComparison.Ordinal);
    }
}
