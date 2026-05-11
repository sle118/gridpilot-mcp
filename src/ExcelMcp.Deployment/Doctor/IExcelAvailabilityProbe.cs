namespace ExcelMcp.Deployment.Doctor;

public interface IExcelAvailabilityProbe
{
    Task<ExcelAvailabilityProbeResult> CheckAsync(
        ExcelAvailabilityProbeRequest request,
        CancellationToken cancellationToken = default);
}
