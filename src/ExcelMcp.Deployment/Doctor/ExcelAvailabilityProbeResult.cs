namespace ExcelMcp.Deployment.Doctor;

public sealed record ExcelAvailabilityProbeResult(
    DoctorCheckSeverity Severity,
    string Message,
    string SuggestedNextStep);
