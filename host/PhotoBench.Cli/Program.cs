using System.Security.Cryptography;
using System.Net.Sockets;
using PhotoBench.Cli;
using PhotoBench.Core.Reporting;
using PhotoBench.Core.Simulation;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("photo-bench run --scenario all --transport tcp --host 127.0.0.1 --port 12345 --output reports/run-id [--manage-simulator] [--root PATH] [--low-limit 16 --high-limit 4079]");
    return 0;
}
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
RenodeRunner? simulator = null;
try
{
    CliOptions options = CliOptions.Parse(args);
    string? root = options.Root;
    if (root == null)
        for (DirectoryInfo? d = new(Environment.CurrentDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "simulation", "platform.repl"))) { root = d.FullName; break; }
    if (root == null) throw new ArgumentException("Run from this repository or supply --root");
    root = Path.GetFullPath(root); string output = Path.GetFullPath(options.Output);
    string elf = Path.Combine(root, "build", "arm", "photo-firmware.elf");
    string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(elf, cancel.Token))).ToLowerInvariant();
    if (options.ManageSimulator)
        simulator = await RenodeRunner.StartAsync(root, Path.Combine(output, "simulator"), options.Port, cancel.Token);
    var metadata = ReleaseMetadata.Create(root, hash, options.Low, options.High);
    var runner = new ScenarioRunner(root, options.Host, simulator?.Port ?? options.Port, metadata, simulator, options.Low, options.High);
    BenchReport report = await runner.RunAsync(options.Scenarios, cancel.Token);
    await ReportWriter.WriteAsync(report, output);
    foreach (ScenarioResult scenario in report.Scenarios) Console.WriteLine($"{scenario.Name}: {scenario.Verdict} — {scenario.Detail}");
    Console.WriteLine($"Overall: {report.OverallVerdict}; {report.Samples.Length} UART results; {Path.Combine(output, "report.html")}");
    return report.OverallVerdict == "PASS" ? 0 : 2;
}
catch (Exception ex) when (ex is ArgumentException or IOException or SocketException or OperationCanceledException)
{
    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 2;
}
finally { if (simulator != null) await simulator.DisposeAsync(); }
