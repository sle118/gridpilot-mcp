using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class SetupElevationPolicyTests
{
    [Theory]
    [InlineData(InstallScope.PerUser, false, false)]
    [InlineData(InstallScope.PerUser, true, false)]
    [InlineData(InstallScope.MachineWide, false, true)]
    [InlineData(InstallScope.MachineWide, true, false)]
    public void RequiresElevation_MatchesInstallScope(InstallScope scope, bool isElevated, bool expected)
    {
        Assert.Equal(expected, SetupElevationPolicy.RequiresElevation(scope, isElevated));
    }
}
