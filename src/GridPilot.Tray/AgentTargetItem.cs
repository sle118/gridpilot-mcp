using ExcelMcp.Deployment.AgentConfig;

namespace GridPilot.Tray;

internal sealed record AgentTargetItem(AgentTarget Target, string DisplayName)
{
    public override string ToString() => DisplayName;
}
