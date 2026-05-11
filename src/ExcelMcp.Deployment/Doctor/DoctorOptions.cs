namespace ExcelMcp.Deployment.Doctor;

public sealed record DoctorOptions
{
    public string? CurrentDirectory { get; init; }

    public bool AllowActiveExcelComProbe { get; init; }
}
