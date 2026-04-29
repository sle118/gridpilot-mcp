using System.Diagnostics.CodeAnalysis;

namespace ExcelMcp.LiveTests.Infrastructure;

internal static class LiveExcelEnvironment
{
    public const string RunLiveTestsVariable = "RUN_LIVE_EXCEL_TESTS";
    public const string WorkbookOverrideVariable = "EXCEL_LIVE_TEST_WORKBOOK";

    public static LiveExcelAvailability GetAvailability()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(false, "Live Excel tests require Windows.");
        }

        if (!string.Equals(Environment.GetEnvironmentVariable(RunLiveTestsVariable), "1", StringComparison.Ordinal))
        {
            return new(false, $"Set {RunLiveTestsVariable}=1 to enable live Excel tests.");
        }

        var workbookPath = ResolveSourceWorkbookPath();
        if (!File.Exists(workbookPath))
        {
            return new(false, $"Live Excel workbook fixture was not found: {workbookPath}");
        }

        return new(true, null);
    }

    public static string ResolveSourceWorkbookPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(WorkbookOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return Path.Combine(FindRepoRoot(), "tests", "live", "fixtures", "test_workbook.xlsx");
    }

    public static string CreateTempWorkbookCopy()
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = Path.Combine(repoRoot, ".tmp", "live-excel");
        Directory.CreateDirectory(tempRoot);

        var fileName = $"test_workbook-{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.xlsx";
        var tempPath = Path.Combine(tempRoot, fileName);

        File.Copy(ResolveSourceWorkbookPath(), tempPath, overwrite: false);
        return tempPath;
    }

    public static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ExcelMcp.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}

internal sealed record LiveExcelAvailability(bool IsAvailable, string? Reason);
