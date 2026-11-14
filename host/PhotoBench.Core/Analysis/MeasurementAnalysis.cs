namespace PhotoBench.Core.Analysis;

public sealed record Statistics(int Count, double? MeanV, double? SampleStddevV, string Status);
public sealed record MeasurementSummary(Statistics Raw, Statistics FilteredSettled,
    int DiscardedStartupSamples, double ClippingRate, double? BaselineSubtractedV);
public sealed record FitPoint(double PowerUw, double MeanV, bool Clipped = false);
public sealed record LinearFit(double SlopeVPerUw, double InterceptV, int Included, int Excluded);

public static class MeasurementAnalysis
{
    public static Statistics Stats(IEnumerable<double> values)
    {
        int n = 0; double mean = 0, m2 = 0;
        foreach (double x in values)
        {
            if (!double.IsFinite(x)) throw new ArgumentException("Nonfinite measurement");
            ++n; double delta = x - mean; mean += delta / n; m2 += delta * (x - mean);
        }
        return new(n, n == 0 ? null : mean, n < 2 ? null : Math.Sqrt(Math.Max(0, m2 / (n - 1))),
                   n < 2 ? "INSUFFICIENT_DATA" : "VALID");
    }
    public static MeasurementSummary Summarise(IReadOnlyList<ushort> raw,
        IReadOnlyList<ushort> filtered, double baselineV = 0)
    {
        if (raw.Count != filtered.Count || !double.IsFinite(baselineV))
            throw new ArgumentException("Mismatched measurements or invalid baseline");
        Statistics rawStats = Stats(raw.Select(PhotoBench.Core.Optics.OpticalModel.ToVolts));
        int discarded = Math.Min(15, filtered.Count);
        Statistics filteredStats = Stats(filtered.Skip(discarded).Select(PhotoBench.Core.Optics.OpticalModel.ToVolts));
        return new(rawStats, filteredStats, discarded,
                   raw.Count == 0 ? 0 : raw.Count(x => x is 0 or 4095) / (double)raw.Count,
                   rawStats.MeanV - baselineV);
    }
    public static LinearFit FitLine(IEnumerable<FitPoint> points)
    {
        FitPoint[] all = points.ToArray();
        if (all.Any(x => !double.IsFinite(x.PowerUw) || !double.IsFinite(x.MeanV)))
            throw new ArgumentException("Nonfinite fit point");
        FitPoint[] valid = all.Where(x => !x.Clipped).ToArray();
        if (valid.Select(x => x.PowerUw).Distinct().Count() < 3)
            throw new ArgumentException("Fit needs three distinct unclipped power levels");
        double mx = valid.Average(x => x.PowerUw), my = valid.Average(x => x.MeanV);
        double slope = valid.Sum(x => (x.PowerUw - mx) * (x.MeanV - my)) /
                       valid.Sum(x => Math.Pow(x.PowerUw - mx, 2));
        return new(slope, my - slope * mx, valid.Length, all.Length - valid.Length);
    }
}
