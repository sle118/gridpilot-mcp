using System.Runtime.Versioning;
using System.Security.Principal;

namespace ExcelMcp.Deployment.Installation;

public static class SetupElevationPolicy
{
    public static bool RequiresElevation(InstallScope scope, bool isElevated) =>
        scope == InstallScope.MachineWide && !isElevated;

    [SupportedOSPlatform("windows")]
    public static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
