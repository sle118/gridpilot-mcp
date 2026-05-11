namespace ExcelMcp.Deployment.Doctor;

public sealed class WritableDirectoryProbe : IWritableDirectoryProbe
{
    public DoctorCheckResult CheckWritable(string directoryPath, string checkId, string checkName)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return Error(checkId, checkName, "Log directory could not be determined.", "Configure an absolute log path or working directory.");
        }

        var createdDirectory = false;
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                createdDirectory = true;
            }

            var probePath = Path.Combine(directoryPath, $".gridpilot-doctor-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);

            if (createdDirectory)
            {
                TryDeleteCreatedDirectory(directoryPath);
            }

            return Ok(checkId, checkName, $"Log directory '{directoryPath}' is writable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Error(
                checkId,
                checkName,
                $"Log directory '{directoryPath}' is not writable: {exception.Message}",
                "Choose a log path in a directory the current user can create and write.");
        }
    }

    private static void TryDeleteCreatedDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _ = exception;
        }
    }

    private static DoctorCheckResult Ok(string id, string name, string message) =>
        new(id, name, DoctorCheckSeverity.Ok, message, "No action needed.");

    private static DoctorCheckResult Error(string id, string name, string message, string nextStep) =>
        new(id, name, DoctorCheckSeverity.Error, message, nextStep);
}
