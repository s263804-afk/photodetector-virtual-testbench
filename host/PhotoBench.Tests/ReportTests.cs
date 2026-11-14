using System.Text.Json;
using PhotoBench.Core.Reporting;
using PhotoBench.Core.Simulation;

namespace PhotoBench.Tests;

public sealed class ReportTests
{
    public static RunMetadata Metadata => new("1.0.0", new string('a', 64), "1.0.0/net10.0",
        JsonSerializer.SerializeToElement(new { Runtime = "10.0.15" }),
        new() { ["noise.json"] = new string('b', 64) }, new() { ["modelSpacingMs"] = 1 });
    private static ScenarioResult Result(string name, string verdict = "PASS") =>
        new(name, verdict, [Guid.NewGuid()], new() { ["meanV"] = .1 }, new() { ["toleranceV"] = .002 }, "observed");

    [Fact]
    public void MissingInterruptedFailedAndDuplicateScenariosCannotPass()
    {
        ScenarioResult[] all = BenchReport.RequiredScenarios.Select(x => Result(x)).ToArray();
        BenchReport Report(ScenarioResult[] scenarios) => new(DateTimeOffset.UtcNow, 2, Metadata, scenarios, [], []);
        Assert.Equal("PASS", Report(all).OverallVerdict);
        Assert.NotEqual("PASS", Report(all[..^1]).OverallVerdict);
        Assert.NotEqual("PASS", Report([.. all, all[0]]).OverallVerdict);
        foreach (string verdict in new[] { "FAIL", "UNVERIFIED", "INTERRUPTED_INDETERMINATE", "SKIPPED" })
            Assert.NotEqual("PASS", Report([Result("dark", verdict), .. all[1..]]).OverallVerdict);
    }

    [Fact]
    public async Task ExportHasTraceableSchemaCsvAndHtml()
    {
        string output = Path.Combine(Path.GetTempPath(), "photobench-report-" + Guid.NewGuid().ToString("N"));
        var report = new BenchReport(DateTimeOffset.UtcNow, 1.25, Metadata,
            BenchReport.RequiredScenarios.Select(x => Result(x)).ToArray(),
            [new("dark", 0, Guid.NewGuid(), 0, 0, 124, 124, 0)], []);
        try
        {
            await ReportWriter.WriteAsync(report, output);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "report.json")));
            foreach (string key in new[] { "firmwareVersion", "firmwareSha256", "hostVersion", "toolVersions", "fixtureHashes", "config" })
                Assert.True(doc.RootElement.GetProperty("metadata").TryGetProperty(key, out _), key);
            Assert.Equal("PASS", doc.RootElement.GetProperty("overallVerdict").GetString());
            Assert.Contains("VIRTUAL_FEED", doc.RootElement.GetProperty("simulationDisclaimer").GetString());
            Assert.Contains("raw,filtered,flags", await File.ReadAllTextAsync(Path.Combine(output, "samples.csv")));
            string html = await File.ReadAllTextAsync(Path.Combine(output, "report.html"));
            Assert.Contains("VIRTUAL_FEED", html); Assert.Contains("dark", html); Assert.Contains("PASS", html);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact, Trait("Category", "ARM")]
    public async Task AllScenariosRunAgainstActualArmAndPassIndependentLimits()
    {
        string root = ArmIntegrationTests.FindRoot(), output = Path.Combine(root, "reports", "example");
        await using var simulator = await RenodeRunner.StartAsync(root, Path.Combine(output, "simulator"));
        var metadata = ReleaseMetadata.Create(root, simulator.ElfSha256);
        string noise = Path.Combine(root, "fixtures", "optical", "noise-20uw.json");
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(noise))).ToLowerInvariant(),
                     metadata.FixtureHashes["optical/noise-20uw.json"]);
        var runner = new ScenarioRunner(root, "127.0.0.1", simulator.Port, metadata, simulator);
        BenchReport report = await runner.RunAsync(BenchReport.RequiredScenarios);
        await ReportWriter.WriteAsync(report, output);
        Assert.All(report.Scenarios, x => Assert.True(x.Verdict == "PASS", x.Name + ": " + x.Detail));
        Assert.Equal("PASS", report.OverallVerdict); Assert.Equal(3968, report.Samples.Length);
        Assert.True(report.Scenarios.SelectMany(x => x.SessionIds).Distinct().Count() >= 10);
    }
}
