namespace ExcelMcp.Deployment.Doctor;

public sealed record DoctorCheckResult(
    string Id,
    string Name,
    DoctorCheckSeverity Severity,
    string Message,
    string SuggestedNextStep);
