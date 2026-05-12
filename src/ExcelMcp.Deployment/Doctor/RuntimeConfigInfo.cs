namespace ExcelMcp.Deployment.Doctor;

public sealed record RuntimeConfigInfo(
    string? FrameworkName,
    string? FrameworkVersion,
    bool UsesIncludedFrameworks = false);
