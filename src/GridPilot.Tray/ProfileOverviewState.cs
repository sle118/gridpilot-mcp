using ExcelMcp.Deployment.Profiles;

namespace GridPilot.Tray;

internal sealed record ProfileOverviewState(
    string? ProfilePath,
    string Status,
    string Details,
    bool CanRunProfileActions,
    LaunchProfile? Profile);
