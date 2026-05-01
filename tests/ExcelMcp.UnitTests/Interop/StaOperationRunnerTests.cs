using ExcelMcp.ComAdapter.Interop;
using ExcelMcp.Core;

namespace ExcelMcp.UnitTests.Interop;

public sealed class StaOperationRunnerTests
{
    [Fact]
    public void Run_ReturnsOperationResult()
    {
        var result = StaOperationRunner.Run(
            () => 42,
            TimeSpan.FromSeconds(1),
            "timeout_code",
            "timeout_message",
            "timeout_detail");

        Assert.Equal(42, result);
    }

    [Fact]
    public void Run_ThrowsStructuredTimeoutWhenOperationDoesNotFinish()
    {
        var exception = Assert.Throws<ExcelSessionTargetException>(() =>
            StaOperationRunner.Run(
                () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                    return 1;
                },
                TimeSpan.FromMilliseconds(50),
                "timeout_code",
                "timeout_message",
                "timeout_detail"));

        Assert.Equal("timeout_code", exception.Code);
        Assert.Equal("timeout_detail", exception.Detail);
    }

    [Fact]
    public void Run_UsesTimeoutDetailFactory()
    {
        var exception = Assert.Throws<ExcelSessionTargetException>(() =>
            StaOperationRunner.Run(
                () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                    return 1;
                },
                TimeSpan.FromMilliseconds(50),
                "timeout_code",
                "timeout_message",
                () => "computed_timeout_detail"));

        Assert.Equal("computed_timeout_detail", exception.Detail);
    }
}
