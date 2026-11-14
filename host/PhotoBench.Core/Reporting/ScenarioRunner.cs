using PhotoBench.Core.Simulation;
using PhotoBench.Core.Analysis;
using PhotoBench.Core.Diagnostics;
using PhotoBench.Core.Optics;
using PhotoBench.Core.Protocol;
using PhotoBench.Core.Session;
using PhotoBench.Core.Transport;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Net.Sockets;

namespace PhotoBench.Core.Reporting;

public sealed class ScenarioRunner(string root, string host, int port, RunMetadata metadata,
                                  RenodeRunner? simulator = null, ushort low = 16, ushort high = 4079)
{
    public async Task<BenchReport> RunAsync(IEnumerable<string> selected,
        CancellationToken cancellationToken = default)
    {
        var names = selected.ToHashSet(StringComparer.Ordinal);
        if (names.Any(x => !BenchReport.RequiredScenarios.Contains(x)) || low >= high || high > 4095)
            throw new ArgumentException("Unknown scenario or invalid limits");
        DateTimeOffset started = DateTimeOffset.UtcNow; var watch = Stopwatch.StartNew();
        var results = new List<ScenarioResult>(); var records = new List<SampleRecord>();
        var trace = new List<TraceEvent>(); var ids = new List<Guid>();
        double baselineV = OpticalModel.ToVolts(124); FitPoint? saturated = null;

        async Task<(ushort[] Raw, ushort[] Filtered, byte[] Flags, MeasurementSummary Summary)> Capture(
            string scenario, ushort[] input, double? power)
        {
            await using var session = new DeviceSession(new TcpTransport(host, port));
            ids.Add(session.SessionId);
            try
            {
                await session.OpenAsync(cancellationToken);
                if (!session.FirmwareIdentity.Split(';').Contains("fw=" + metadata.FirmwareVersion))
                    throw new InvalidDataException("Unexpected firmware identity: " + session.FirmwareIdentity);
                Frame limits = await session.ExecuteAsync("SET_LIMITS", $"{low},{high}", cancellationToken);
                Frame start = await session.ExecuteAsync("START", cancellationToken: cancellationToken);
                if (limits.Command != "OK" || start.Command != "OK") throw new InvalidDataException("Firmware rejected setup");
                var raw = new List<ushort>(); var filtered = new List<ushort>(); var flags = new List<byte>();
                ushort[] expected = ReferenceFilter.Process(input);
                for (int offset = 0; offset < input.Length; offset += 16)
                {
                    int count = Math.Min(16, input.Length - offset);
                    string args = string.Join(',', input.Skip(offset).Take(count));
                    Frame reply = await session.ExecuteAsync("FEED", args, cancellationToken);
                    if (reply.Command != "DATA") throw new InvalidDataException("FEED rejected: " + reply.Command);
                    string[] fields = reply.Arguments.Split(';');
                    if (fields.Length != count + 1 || uint.Parse(fields[0], CultureInfo.InvariantCulture) != (uint)offset)
                        throw new InvalidDataException("Missing/duplicate/misindexed sample batch");
                    for (int j = 0; j < count; ++j)
                    {
                        string[] tuple = fields[j + 1].Split(':');
                        if (tuple.Length != 3) throw new InvalidDataException("Invalid sample tuple");
                        ushort r = ushort.Parse(tuple[0], CultureInfo.InvariantCulture), f = ushort.Parse(tuple[1], CultureInfo.InvariantCulture);
                        byte flag = byte.Parse(tuple[2], CultureInfo.InvariantCulture);
                        int index = offset + j;
                        byte expectedFlags = (byte)((input[index] <= low ? 1 : 0) | (input[index] >= high ? 2 : 0));
                        records.Add(new(scenario, power, session.SessionId, (uint)index, index, r, f, flag));
                        if (r != input[index] || f != expected[index] || flag != expectedFlags)
                            throw new InvalidDataException($"ARM/reference mismatch at sample{index}: {r}:{f}:{flag}");
                        raw.Add(r); filtered.Add(f); flags.Add(flag);
                    }
                }
                Frame status = await session.ExecuteAsync("STATUS", cancellationToken: cancellationToken);
                if (CommunicationChecks.ParseStatus(status.Arguments)["processed"] != input.Length.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidDataException("Processed count differs from accepted samples");
                await session.ExecuteAsync("STOP", cancellationToken: cancellationToken);
                return (raw.ToArray(), filtered.ToArray(), flags.ToArray(), MeasurementAnalysis.Summarise(raw, filtered, baselineV));
            }
            finally { trace.AddRange(session.Trace); }
        }

        // Saturation precedes linearity so the excluded point is an observed UART result.
        foreach (string name in new[] { "dark", "light_step", "noise", "saturation", "linearity", "communication_recovery" }.Where(names.Contains))
        {
            ids.Clear(); var metrics = new Dictionary<string, object>(); var limits = new Dictionary<string, object>();
            string detail = "Every raw/filtered/flag tuple matched the independent reference."; bool pass = true;
            try
            {
                switch (name)
                {
                    case "dark":
                    {
                        var data = await Capture(name, OpticalModel.Generate(new(0, 1024)).Samples, 0);
                        baselineV = data.Summary.Raw.MeanV!.Value;
                        pass = data.Raw.All(x => x == 124) && data.Flags.All(x => x == 0) && Math.Abs(baselineV - .1) <= 3.3 / 4095;
                        metrics["summary"] = data.Summary; limits["rawCode"] = 124; limits["baselineToleranceV"] = 3.3 / 4095;
                        break;
                    }
                    case "light_step":
                    {
                        ushort[] input = [.. Enumerable.Repeat((ushort)124, 64), .. Enumerable.Repeat((ushort)869, 64)];
                        var data = await Capture(name, input, null);
                        pass = data.Filtered[64] == 170 && data.Filtered[79] == 869;
                        metrics["firstChangedFiltered"] = data.Filtered[64]; metrics["sixteenthChangedFiltered"] = data.Filtered[79];
                        metrics["input"] = "64x124 (0uW), then64x869 (10uW)";
                        limits["firstChangedFiltered"] = 170; limits["sixteenthChangedFiltered"] = 869;
                        break;
                    }
                    case "noise":
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "fixtures", "optical", "noise-20uw.json")));
                        var vector = doc.RootElement;
                        ushort[] input = vector.GetProperty("samples").EnumerateArray().Select(x => x.GetUInt16()).ToArray();
                        var data = await Capture(name, input, 20);
                        var errors = new Dictionary<string, double>();
                        foreach (var (key, stats) in new[] { ("raw", data.Summary.Raw), ("filtered_settled", data.Summary.FilteredSettled) })
                        {
                            var reference = vector.GetProperty("reference").GetProperty(key);
                            errors[key + "MeanErrorV"] = Math.Abs(stats.MeanV!.Value - reference.GetProperty("mean_v").GetDouble());
                            errors[key + "StdErrorV"] = Math.Abs(stats.SampleStddevV!.Value - reference.GetProperty("sample_stddev_v").GetDouble());
                        }
                        pass = errors.Values.All(x => x <= 1e-6);
                        metrics["summary"] = data.Summary; metrics["referenceErrorsV"] = errors; limits["referenceToleranceV"] = 1e-6;
                        detail += " Noise fixture and Python sample statistics are saved separately; no ideal sqrt(16) reduction claim.";
                        break;
                    }
                    case "saturation":
                    {
                        var data = await Capture(name, OpticalModel.Generate(new(60, 256)).Samples, 60);
                        saturated = new(60, data.Summary.FilteredSettled.MeanV!.Value, data.Summary.ClippingRate > 0);
                        pass = data.Raw.All(x => x == 4095) && data.Flags.All(x => (x & 2) != 0);
                        metrics["summary"] = data.Summary; metrics["highFlagCount"] = data.Flags.Count(x => (x & 2) != 0);
                        metrics["excludedFromLinearity"] = true; limits["rawCode"] = 4095; limits["allHighFlags"] = true;
                        break;
                    }
                    case "linearity":
                    {
                        var points = new List<FitPoint>(); var summaries = new List<object>();
                        foreach (double power in new double[] { 0, 5, 10, 20, 30, 40 })
                        {
                            var data = await Capture(name, OpticalModel.Generate(new(power, 256)).Samples, power);
                            points.Add(new(power, data.Summary.FilteredSettled.MeanV!.Value, data.Summary.ClippingRate > 0));
                            summaries.Add(new { PowerUw = power, data.Summary });
                        }
                        if (saturated != null) points.Add(saturated);
                        LinearFit fit = MeasurementAnalysis.FitLine(points);
                        pass = Math.Abs(fit.SlopeVPerUw - .060) <= .0005 && Math.Abs(fit.InterceptV - .100) <= .002;
                        metrics["fit"] = fit; metrics["levels"] = summaries; metrics["excludedPowerLevelsUw"] = points.Where(x => x.Clipped).Select(x => x.PowerUw).ToArray();
                        limits["slopeVPerUw"] = .060; limits["slopeTolerance"] = .0005; limits["interceptV"] = .100; limits["interceptToleranceV"] = .002;
                        break;
                    }
                    case "communication_recovery":
                    {
                        CommunicationReport report = await CommunicationChecks.RunAsync(host, port, simulator, cancellationToken);
                        ids.AddRange(report.SessionIds); trace.AddRange(report.Trace); metrics["checks"] = report.Checks;
                        limits["allRequiredChecks"] = "PASS"; pass = report.Checks.All(x => x.Passed);
                        detail = "Deliberate byte-delivery faults on actual C#↔TCP↔ARM path; reset preserves TCP.";
                        break;
                    }
                }
                results.Add(new(name, pass ? "PASS" : "FAIL", ids.ToArray(), metrics, limits, detail));
            }
            catch (Exception ex) when (ex is IOException or SocketException or ArgumentException or OperationCanceledException or FormatException)
            {
                string verdict = ex is SessionInterruptedException ? "INTERRUPTED_INDETERMINATE" :
                    ex is OperationCanceledException ? "INTERRUPTED_CANCELLED" : "FAIL";
                if (ex is CommunicationInterruptedException interrupted)
                {
                    ids.AddRange(interrupted.Partial.SessionIds); trace.AddRange(interrupted.Partial.Trace);
                    metrics["checks"] = interrupted.Partial.Checks; verdict = interrupted.Outcome;
                }
                results.Add(new(name, verdict,
                    ids.ToArray(), metrics, limits, ex.GetType().Name + ": " + ex.Message));
                if (cancellationToken.IsCancellationRequested) break;
            }
        }
        return new(started, watch.Elapsed.TotalSeconds, metadata, results.ToArray(), records.ToArray(), trace.ToArray());
    }
}
