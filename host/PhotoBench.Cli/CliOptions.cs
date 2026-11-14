using PhotoBench.Core.Reporting;
using System.Globalization;

namespace PhotoBench.Cli;

public sealed record CliOptions(string[] Scenarios, string Host, int Port, string Output,
    bool ManageSimulator, string? Root, ushort Low, ushort High)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "run") throw new ArgumentException("Expected: photo-bench run ...");
        var values = new Dictionary<string, string>(); bool manage = false;
        string[] allowed = ["--scenario", "--transport", "--host", "--port", "--output", "--root", "--low-limit", "--high-limit"];
        for (int i = 1; i < args.Length; ++i)
        {
            string key = args[i];
            if (key == "--manage-simulator") { manage = true; continue; }
            if (!allowed.Contains(key) || i + 1 >= args.Length || !values.TryAdd(key, args[++i]))
                throw new ArgumentException("Unknown, missing or duplicate option: " + key);
        }
        if (values.GetValueOrDefault("--transport", "tcp") != "tcp") throw new ArgumentException("Only TCP is implemented");
        string scenario = values.GetValueOrDefault("--scenario", "all");
        string[] selected = scenario == "all" ? BenchReport.RequiredScenarios.ToArray() : scenario.Split(',');
        if (selected.Any(x => !BenchReport.RequiredScenarios.Contains(x)) || selected.Distinct().Count() != selected.Length)
            throw new ArgumentException("Unknown or duplicate scenario");
        if (!int.TryParse(values.GetValueOrDefault("--port", "12345"), NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535 ||
            !ushort.TryParse(values.GetValueOrDefault("--low-limit", "16"), out ushort low) ||
            !ushort.TryParse(values.GetValueOrDefault("--high-limit", "4079"), out ushort high) || low >= high || high > 4095)
            throw new ArgumentException("Invalid port or alarm limits");
        return new(selected, values.GetValueOrDefault("--host", "127.0.0.1"), port,
            values.GetValueOrDefault("--output", "reports/run-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)),
            manage, values.GetValueOrDefault("--root"), low, high);
    }
}
