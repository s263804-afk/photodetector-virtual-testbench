using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace PhotoBench.Core.Simulation;

public sealed class RenodeRunner : IAsyncDisposable
{
    private Process? process;
    private readonly ConcurrentQueue<string> log = new();
    private string stage = "", evidence = "";
    public int Port { get; private set; }
    public string ElfSha256 { get; private set; } = "";
    public static async Task<RenodeRunner> StartAsync(string root, string evidenceDirectory, int port = 0,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Pinned Renode launcher targets Windows");
        var runner = new RenodeRunner { evidence = Path.GetFullPath(evidenceDirectory) };
        Directory.CreateDirectory(runner.evidence);
        string tools = Environment.GetEnvironmentVariable("PHOTOBENCH_TOOLS") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortfolioTools");
        runner.stage = Path.Combine(tools, "photobench-runs", Guid.NewGuid().ToString("N"));
        if (runner.stage.Any(char.IsWhiteSpace) || runner.stage.Contains(','))
            throw new ArgumentException("PHOTOBENCH_TOOLS must have a path without spaces or commas (Renode parser limitation)");
        Directory.CreateDirectory(Path.Combine(runner.stage, ".local"));
        if (port == 0)
        {
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start(); port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        }
        runner.Port = port;
        string config = Path.Combine(runner.stage, ".local", "renode-config");
        await File.WriteAllTextAsync(config, "[general]\nhistory-path = " +
            Path.Combine(runner.stage, ".local", "monitor-history") + "\ncompiler-cache-enabled = False\n", cancellationToken);
        string[] artifacts = ["simulation/platform.resc", "simulation/platform.repl",
                              "simulation/LoopbackTerminal.cs", "build/arm/photo-firmware.elf"];
        foreach (string relative in artifacts)
        {
            string source = Path.Combine(root, relative), destination = Path.Combine(runner.stage, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (relative.EndsWith(".resc", StringComparison.Ordinal))
            {
                string text = File.ReadAllText(source).Replace("12345", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (string prefix in new[] { "simulation", "build", ".local" })
                    text = text.Replace("@" + prefix + "/", "@" + Path.Combine(runner.stage, prefix).Replace('\\', '/') + "/");
                await File.WriteAllTextAsync(destination, text, cancellationToken);
            }
            else File.Copy(source, destination, true);
        }
        runner.ElfSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(runner.stage, "build/arm/photo-firmware.elf")))).ToLowerInvariant();
        string executable = Path.Combine(tools, "renode-1.17.0", "renode_1.17.0-portable", "renode.exe");
        var start = new ProcessStartInfo(executable) {
            WorkingDirectory = runner.stage, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "--disable-xwt", "--hide-analyzers", "--plain", "--console", "--config", config, "simulation/platform.resc" })
            start.ArgumentList.Add(arg);
        runner.process = new Process { StartInfo = start };
        runner.process.OutputDataReceived += (_, e) => { if (e.Data != null) runner.log.Enqueue(e.Data); };
        runner.process.ErrorDataReceived += (_, e) => { if (e.Data != null) runner.log.Enqueue(e.Data); };
        runner.process.Start(); runner.process.BeginOutputReadLine(); runner.process.BeginErrorReadLine();
        try
        {
            var elapsed = Stopwatch.StartNew();
            while (!runner.log.Any(x => x.Contains("Machine started", StringComparison.Ordinal)))
            {
                if (runner.process.HasExited || elapsed.Elapsed > TimeSpan.FromSeconds(30))
                    throw new IOException("Renode did not boot: " + string.Join("\n", runner.log));
                await Task.Delay(50, cancellationToken);
            }
            await Task.Delay(100, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(runner.evidence, "simulator.json"), JsonSerializer.Serialize(new {
                runner.Port, runner.ElfSha256, Renode = "1.17.0", Platform = "project STM32F407VG UART subset",
                Stimulus = "VIRTUAL_FEED; not an ADC peripheral", Monitor = "private stdin"
            }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            return runner;
        }
        catch { await runner.DisposeAsync(); throw; }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited) throw new IOException("Simulator is not running");
        await process.StandardInput.WriteLineAsync("machine Reset".AsMemory(), cancellationToken);
        await process.StandardInput.WriteLineAsync("start".AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
        await Task.Delay(300, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (process != null)
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync("quit");
                await process.StandardInput.FlushAsync();
                using var timeout = new CancellationTokenSource(5000);
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); }
            }
            await File.WriteAllLinesAsync(Path.Combine(evidence, "renode.log"), log);
            string uart = Path.Combine(stage, ".local", "uart-production.log");
            if (File.Exists(uart)) File.Copy(uart, Path.Combine(evidence, "uart.log"), true);
            process.Dispose(); process = null;
        }
    }
}
