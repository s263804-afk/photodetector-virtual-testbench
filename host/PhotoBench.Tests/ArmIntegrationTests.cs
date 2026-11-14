using System.Text.Json;
using PhotoBench.Core.Diagnostics;
using PhotoBench.Core.Simulation;

namespace PhotoBench.Tests;

public sealed class ArmIntegrationTests
{
    public static string FindRoot()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "simulation", "platform.repl"))) return d.FullName;
        throw new DirectoryNotFoundException("Run tests from the complete repository");
    }

    [Fact, Trait("Category", "ARM")]
    public async Task ActualArmFirmwarePassesTcpFaultAndRetainedConnectionResetChecks()
    {
        string root = FindRoot(), evidence = Path.Combine(root, "reports", "evidence", "e6-arm");
        await using var simulator = await RenodeRunner.StartAsync(root, evidence);
        var result = await CommunicationChecks.RunAsync("127.0.0.1", simulator.Port, simulator);
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "communication.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(64, simulator.ElfSha256.Length);
        Assert.True(result.Checks.Length >= 7);
        Assert.All(result.Checks, x => Assert.True(x.Passed, x.Name + ": " + x.Detail));
        Assert.Contains(result.Checks, x => x.Name == "RESET_TCP_RETAINED");
        Assert.Contains(result.Checks, x => x.Name == "LOST_FEED_REPLY_EXACTLY_ONCE");
        Assert.Contains(result.Checks, x => x.Name == "ALL_REPLIES_LOST_INDETERMINATE");
        Assert.True(result.SessionIds.Distinct().Count() >= 4);
    }
}
