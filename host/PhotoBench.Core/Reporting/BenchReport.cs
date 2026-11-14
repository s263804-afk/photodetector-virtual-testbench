using System.Text.Json;
using PhotoBench.Core.Session;
using System.Globalization;
using System.Net;
using System.Text;

namespace PhotoBench.Core.Reporting;

public sealed record RunMetadata(string FirmwareVersion, string FirmwareSha256, string HostVersion,
    JsonElement ToolVersions, Dictionary<string, string> FixtureHashes, Dictionary<string, object> Config);
public sealed record ScenarioResult(string Name, string Verdict, Guid[] SessionIds,
    Dictionary<string, object> Metrics, Dictionary<string, object> Limits, string Detail);
public sealed record SampleRecord(string Scenario, double? PowerUw, Guid SessionId, uint Index,
    double ModelTimeMs, ushort Raw, ushort Filtered, byte Flags);
public sealed record BenchReport(DateTimeOffset StartedUtc, double WallTimeSeconds,
    RunMetadata Metadata, ScenarioResult[] Scenarios, SampleRecord[] Samples, TraceEvent[] Trace)
{
    public static readonly string[] RequiredScenarios = ["dark", "light_step", "linearity", "noise", "saturation", "communication_recovery"];
    public string SimulationDisclaimer => "ARM instructions execute in Renode. Synthetic codes enter a VIRTUAL_FEED software boundary; no ADC, optical device or physical timing is validated.";
    public string OverallVerdict => Scenarios.Any(x => x.Verdict == "FAIL") ? "FAIL" :
        Scenarios.Length == RequiredScenarios.Length &&
        RequiredScenarios.All(name => Scenarios.Count(x => x.Name == name && x.Verdict == "PASS") == 1)
            ? "PASS" : "INCOMPLETE";
}

public static class ReportWriter
{
    public static async Task WriteAsync(BenchReport report, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "report.json"), JsonSerializer.Serialize(report, options), cancellationToken);
        var csv = new StringBuilder("scenario,power_uW,session_id,index,model_time_ms,raw,filtered,flags\n");
        foreach (SampleRecord x in report.Samples)
            csv.Append(CultureInfo.InvariantCulture, $"{x.Scenario},{x.PowerUw},{x.SessionId},{x.Index},{x.ModelTimeMs},{x.Raw},{x.Filtered},{x.Flags}\n");
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "samples.csv"), csv.ToString(), cancellationToken);
        static string H(string value) => WebUtility.HtmlEncode(value);
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Photodetector virtual test report</title>");
        html.Append("<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 24px;color:#18283b;background:#f6f8fb}h1,h2{color:#123d66}.card{background:white;padding:24px;border-radius:12px;margin:18px 0;border:1px solid #dbe4ee}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px}code{overflow-wrap:anywhere}.PASS{color:#127b46}.FAIL,.INCOMPLETE{color:#a42f32}table{border-collapse:collapse;width:100%}td,th{padding:10px;border-bottom:1px solid #dde3ea;text-align:left}</style>");
        html.Append($"<h1>Photodetector virtual test bench</h1><p class=\"{report.OverallVerdict}\"><b>{report.OverallVerdict}</b> · {report.StartedUtc:O}</p>");
        html.Append($"<div class=\"card\"><p>{H(report.SimulationDisclaimer)}</p><p>Independent portfolio project by Weitong Liu, developed with AI assistance.</p><p>Firmware {H(report.Metadata.FirmwareVersion)} · host {H(report.Metadata.HostVersion)}</p><code>ELF SHA256 {H(report.Metadata.FirmwareSha256)}</code><p>{report.Samples.Length} actual UART sample results · wall time {report.WallTimeSeconds.ToString("F3", CultureInfo.InvariantCulture)} s. Model spacing is separate from wall time.</p></div>");
        foreach (ScenarioResult scenario in report.Scenarios)
        {
            html.Append($"<section class=\"card\"><h2>{H(scenario.Name)} — <span class=\"{H(scenario.Verdict)}\">{H(scenario.Verdict)}</span></h2><p>{H(scenario.Detail)}</p><h3>Observed metrics</h3><pre>{H(JsonSerializer.Serialize(scenario.Metrics, options))}</pre><h3>Project test limits</h3><pre>{H(JsonSerializer.Serialize(scenario.Limits, options))}</pre><p>Sessions: {H(string.Join(", ", scenario.SessionIds))}</p></section>");
        }
        html.Append($"<section class=\"card\"><h2>Reproduction metadata</h2><pre>{H(JsonSerializer.Serialize(report.Metadata, options))}</pre></section><p>Full machine-readable trace: report.json. Raw and filtered samples: samples.csv.</p></html>");
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "report.html"), html.ToString(), cancellationToken);
    }
}
