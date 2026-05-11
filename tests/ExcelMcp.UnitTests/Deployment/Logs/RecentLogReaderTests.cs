using ExcelMcp.Deployment.Logs;

namespace ExcelMcp.UnitTests.Deployment.Logs;

public sealed class RecentLogReaderTests
{
    [Fact]
    public async Task ReadTailAsync_MissingLogReturnsStructuredMissingResult()
    {
        var path = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"), "missing.log");

        var result = await RecentLogReader.ReadTailAsync(path);

        Assert.False(result.Exists);
        Assert.False(result.IsSuccess);
        Assert.Equal(DeploymentLogAccessStatus.Missing, result.AccessStatus);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task ReadTailAsync_EmptyLogSucceedsWithNoLines()
    {
        using var temp = TestLogWorkspace.Create();
        var path = temp.WriteLog("empty.log", string.Empty);

        var result = await RecentLogReader.ReadTailAsync(path);

        Assert.True(result.Exists);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Lines);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public async Task ReadTailAsync_LargeLogRespectsLineAndByteBounds()
    {
        using var temp = TestLogWorkspace.Create();
        var lines = Enumerable.Range(1, 200).Select(index => $"line-{index:000}");
        var path = temp.WriteLog("large.log", string.Join("\n", lines));

        var result = await RecentLogReader.ReadTailAsync(path, new RecentLogReadOptions(MaxLines: 3, MaxBytes: 128));

        Assert.True(result.IsSuccess);
        Assert.True(result.WasTruncated);
        Assert.Equal(["line-198", "line-199", "line-200"], result.Lines);
    }

    [Fact]
    public async Task ReadTailAsync_NormalizesLineEndings()
    {
        using var temp = TestLogWorkspace.Create();
        var path = temp.WriteLog("line-endings.log", "one\r\ntwo\rthree\n");

        var result = await RecentLogReader.ReadTailAsync(path);

        Assert.Equal(["one", "two", "three"], result.Lines);
    }

    [Fact]
    public async Task ReadTailAsync_LockedLogReturnsStructuredFailure()
    {
        using var temp = TestLogWorkspace.Create();
        var path = temp.WriteLog("locked.log", "locked");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await RecentLogReader.ReadTailAsync(path);

        Assert.True(result.Exists);
        Assert.False(result.IsSuccess);
        Assert.Equal(DeploymentLogAccessStatus.Unreadable, result.AccessStatus);
        Assert.Empty(result.Lines);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    private sealed class TestLogWorkspace : IDisposable
    {
        private TestLogWorkspace(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static TestLogWorkspace Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new TestLogWorkspace(directoryPath);
        }

        public string WriteLog(string fileName, string content)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
