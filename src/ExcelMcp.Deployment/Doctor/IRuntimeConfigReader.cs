namespace ExcelMcp.Deployment.Doctor;

public interface IRuntimeConfigReader
{
    RuntimeConfigInfo Read(string path);
}
