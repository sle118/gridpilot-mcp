namespace ExcelMcp.Deployment.Doctor;

public sealed record DoctorReport(IReadOnlyList<DoctorCheckResult> Results)
{
    public bool HasErrors => Results.Any(result => result.Severity == DoctorCheckSeverity.Error);
}
