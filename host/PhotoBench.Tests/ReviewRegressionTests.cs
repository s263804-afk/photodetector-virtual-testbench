using System.Net;
using System.Net.Sockets;
using PhotoBench.Core.Protocol;
using PhotoBench.Core.Reporting;

namespace PhotoBench.Tests;

public sealed class ReviewRegressionTests
{
    [Fact]
    public async Task RefusedEndpointProducesFailureReportBeforeAnyTransmission()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var runner = new ScenarioRunner(ArmIntegrationTests.FindRoot(), "127.0.0.1", port, ReportTests.Metadata);
        BenchReport report = await runner.RunAsync(["dark"]);
        Assert.Equal("FAIL", report.Scenarios.Single().Verdict);
        Assert.Contains("SocketException", report.Scenarios.Single().Detail);
        Assert.Empty(report.Samples); Assert.NotEqual("PASS", report.OverallVerdict);
        Assert.DoesNotContain(report.Trace, x => x.Event == "SEND");
    }

    [Fact]
    public async Task RefusalAfterCompletedScenarioPreservesItsSamplesAndTrace()
    {
        await using var fixture = new LoopbackFixture(false);
        var runner = new ScenarioRunner(ArmIntegrationTests.FindRoot(), "127.0.0.1", fixture.Port, ReportTests.Metadata);
        BenchReport report = await runner.RunAsync(["dark", "noise"]);
        Assert.Equal("PASS", report.Scenarios[0].Verdict); Assert.Equal("FAIL", report.Scenarios[1].Verdict);
        Assert.Equal(1024, report.Samples.Length); Assert.NotEmpty(report.Trace);
        Assert.Contains("SocketException", report.Scenarios[1].Detail);
    }

    [Fact]
    public async Task CommunicationCancellationPreservesCompletedChecksAndInterruptedSession()
    {
        using var cancel = new CancellationTokenSource();
        await using var fixture = new LoopbackFixture(true, cancel);
        var runner = new ScenarioRunner(ArmIntegrationTests.FindRoot(), "127.0.0.1", fixture.Port, ReportTests.Metadata);
        BenchReport report = await runner.RunAsync(["communication_recovery"], cancel.Token);
        ScenarioResult scenario = report.Scenarios.Single();
        Assert.Equal("INTERRUPTED_INDETERMINATE", scenario.Verdict);
        Assert.True(scenario.Metrics.ContainsKey("checks"));
        var checks = Assert.IsType<PhotoBench.Core.Diagnostics.CheckResult[]>(scenario.Metrics["checks"]);
        Assert.True(checks.Length >= 4); Assert.NotEmpty(scenario.SessionIds);
        Assert.Contains(report.Trace, x => x.Event == "SEND" &&
            System.Text.Encoding.ASCII.GetString(Convert.FromHexString(x.Detail)).Contains("|START|"));
        Assert.Contains(report.Trace, x => x.Event == "SESSION_INVALIDATED");
        Assert.NotEqual("PASS", report.OverallVerdict);
    }

    // A deliberately narrow TCP fixture tests host report preservation, not ARM correctness.
    // Dark samples have the independently known constant124 result. ARM scenarios are separate.
    private sealed class LoopbackFixture : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task worker;
        public int Port { get; }
        public LoopbackFixture(bool communication, CancellationTokenSource? cancel = null)
        {
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            worker = Task.Run(async () => {
                try
                {
                    for (int connection = 0; connection < (communication ? 2 : 1); ++connection)
                    {
                        using TcpClient peer = await listener.AcceptTcpClientAsync(stop.Token);
                        if (!communication) listener.Stop(); // Keep this accepted stream; refuse all later scenarios.
                        var parser = new FrameParser(); uint processed = 0; Frame? cached = null;
                        byte[] bytes = new byte[2048];
                        while (true)
                        {
                            int n = await peer.GetStream().ReadAsync(bytes, stop.Token); if (n == 0) break;
                            foreach (Frame request in parser.Push(bytes.AsSpan(0, n)))
                            {
                                if (communication && connection == 1 && request.Command == "START")
                                {
                                    cancel!.Cancel(); await Task.Delay(100, stop.Token); break;
                                }
                                string command = "OK", arguments = "-";
                                switch (request.Command)
                                {
                                    case "HELLO": command = "HELLO"; arguments = "fw=1.0.0;protocol=1;mode=VIRTUAL_FEED"; break;
                                    case "PING": command = "PONG"; break;
                                    case "FEED":
                                        if (communication && cached != null) { command = cached.Command; arguments = cached.Arguments; break; }
                                        command = "DATA";
                                        if (communication) arguments = "0;124:124:0;869:496:0;4095:1696:2";
                                        else arguments = processed + ";" + string.Join(';', request.Arguments.Split(',').Select(_ => "124:124:0"));
                                        processed += (uint)request.Arguments.Split(',').Length;
                                        cached = new(request.Sequence, command, arguments); break;
                                    case "STATUS": command = "STATUS"; arguments = $"state=RUNNING;processed={processed};crc_errors={parser.CrcErrors};overflow_errors={parser.OverflowErrors};errors=0"; break;
                                }
                                await peer.GetStream().WriteAsync(FrameCodec.Encode(new(request.Sequence, command, arguments)), stop.Token);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            });
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await worker; stop.Dispose(); }
    }
}
