using ExcelMcp.Core.Logging;
using System.Text.Json;

namespace ExcelMcp.UnitTests.Logging;

public sealed class GridPilotFileLoggerTests
{
    [Fact]
    public async Task NullLogger_DoesNotCreateFile()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDirectory, "runtime.log");

        await using var logger = GridPilotLoggerFactory.Create(GridPilotLogLevel.Off, logPath);
        logger.LogInfo("Test", "ignored");

        Assert.False(File.Exists(logPath));
    }

    [Fact]
    public async Task FileLogger_CreatesDirectoryAndWritesJsonLine()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDirectory, "runtime.log");

        await using (var logger = GridPilotLoggerFactory.Create(GridPilotLogLevel.Info, logPath))
        {
            logger.LogInfo("WorkbookService", "inventory_listed", new Dictionary<string, object?>
            {
                ["workbookPath"] = @"C:\temp\book.xlsx",
                ["sheetCount"] = 2
            });
        }

        var line = Assert.Single(await File.ReadAllLinesAsync(logPath));
        using var document = JsonDocument.Parse(line);
        Assert.Equal("info", document.RootElement.GetProperty("level").GetString());
        Assert.Equal("WorkbookService", document.RootElement.GetProperty("category").GetString());
        Assert.Equal("inventory_listed", document.RootElement.GetProperty("event").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("sheetCount").GetInt32());
    }

    [Fact]
    public async Task FileLogger_SerializesConcurrentWritesAsSingleLines()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDirectory, "runtime.log");

        await using (var logger = GridPilotLoggerFactory.Create(GridPilotLogLevel.Trace, logPath))
        {
            await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(() =>
                logger.LogTrace("Concurrent", "write", new Dictionary<string, object?> { ["index"] = index }))));
        }

        var lines = await File.ReadAllLinesAsync(logPath);
        Assert.Equal(20, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("{", line, StringComparison.Ordinal));
    }
}
