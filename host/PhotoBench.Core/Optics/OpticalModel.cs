namespace PhotoBench.Core.Optics;

public sealed record ScenarioConfig(double PowerUw, int Count, double NoiseSigmaV = 0,
                                    int Seed = 20261112, double ModelSpacingMs = 1);
public sealed record SampleVector(ScenarioConfig Config, ushort[] Samples);

public static class OpticalModel
{
    public const double Vref = 3.3;
    public static ushort ToCode(double voltage)
    {
        if (!double.IsFinite(voltage)) throw new ArgumentException("Voltage must be finite");
        return (ushort)Math.Floor(4095 * Math.Clamp(voltage, 0, Vref) / Vref + .5);
    }
    public static double ToVolts(ushort code)
    {
        if (code > 4095) throw new ArgumentException("Code exceeds 12-bit range");
        return code * Vref / 4095;
    }
    public static SampleVector Generate(ScenarioConfig config)
    {
        if (!double.IsFinite(config.PowerUw) || config.PowerUw < 0 ||
            config.Count is < 1 or > 1_000_000 || !double.IsFinite(config.NoiseSigmaV) ||
            config.NoiseSigmaV < 0 || !double.IsFinite(config.ModelSpacingMs) || config.ModelSpacingMs <= 0)
            throw new ArgumentException("Invalid optical scenario configuration");
        double idealV = .100 + 100_000 * .6 * (config.PowerUw * 1e-6);
        if (!double.IsFinite(idealV)) throw new ArgumentException("Power overflows model voltage");
        var random = new Random(config.Seed);
        var samples = new ushort[config.Count];
        for (int i = 0; i < samples.Length; ++i)
        {
            // Box-Muller; saved fixtures, rather than runtime PRNG identity, define release evidence.
            double noise = config.NoiseSigmaV == 0 ? 0 : config.NoiseSigmaV *
                Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
            samples[i] = ToCode(idealV + noise);
        }
        return new(config, samples);
    }
}

public static class ReferenceFilter
{
    public static ushort[] Process(IReadOnlyList<ushort> samples)
    {
        if (samples.Any(x => x > 4095)) throw new ArgumentException("Invalid reference sample");
        var result = new ushort[samples.Count];
        // Intentionally direct O(16n) windows, independent of the firmware's rolling ring sum.
        for (int i = 0; i < samples.Count; ++i)
        {
            int first = Math.Max(0, i - 15), sum = 0;
            for (int j = first; j <= i; ++j) sum += samples[j];
            result[i] = (ushort)(sum / (i - first + 1));
        }
        return result;
    }
}
