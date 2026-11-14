using PhotoBench.Cli;
using PhotoBench.Core.Reporting;

namespace PhotoBench.Tests;

public sealed class CliTests
{
    [Fact]
    public void DocumentedCommandSelectsAllSixGroupsAndExplicitEndpoint()
    {
        var options = CliOptions.Parse(["run", "--scenario", "all", "--transport", "tcp", "--host", "127.0.0.1",
            "--port", "12345", "--output", "reports/run-id", "--manage-simulator"]);
        Assert.Equal(BenchReport.RequiredScenarios, options.Scenarios); Assert.Equal(12345, options.Port);
        Assert.Equal("reports/run-id", options.Output); Assert.True(options.ManageSimulator);
    }
    [Theory]
    [InlineData("--transport", "serial")] [InlineData("--port", "0")]
    [InlineData("--scenario", "made_up")] [InlineData("--low-limit", "4095")]
    [InlineData("--unknown", "x")]
    public void InvalidCommandIsRejectedBeforeConnecting(string flag, string value) =>
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["run", flag, value]));
}
