namespace ExcelMcp.Deployment.Doctor;

public interface IWritableDirectoryProbe
{
    DoctorCheckResult CheckWritable(string directoryPath, string checkId, string checkName);
}
