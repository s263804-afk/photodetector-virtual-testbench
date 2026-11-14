using System.Text.Json;
using PhotoBench.Core.Optics;
using PhotoBench.Core.Analysis;

namespace PhotoBench.Tests;

public sealed class OpticsTests
{
    [Theory]
    [InlineData(0, 124)] [InlineData(10, 869)] [InlineData(60, 4095)]
    public void ModelUsesWattsAndRoundsClippedVoltage(double power, int expected)
    {
        var vector = OpticalModel.Generate(new(power, 32));
        Assert.All(vector.Samples, x => Assert.Equal(expected, x));
    }

    [Fact]
    public void InvalidModelConfigurationCannotProduceSamples()
    {
        foreach (var config in new[] { new ScenarioConfig(-1, 3), new(double.NaN, 3),
                 new(1, 0), new(1, 3, -1), new(1, 3, double.PositiveInfinity), new(1, 3, 0, 1, 0) })
            Assert.Throws<ArgumentException>(() => OpticalModel.Generate(config));
        Assert.Throws<ArgumentException>(() => OpticalModel.ToCode(double.NaN));
        Assert.Throws<ArgumentException>(() => OpticalModel.ToVolts(4096));
    }

    [Fact]
    public void DirectWindowReferenceHasKnownStartupAndStepResults()
    {
        Assert.Equal(new ushort[] { 100, 150, 200 }, ReferenceFilter.Process(new ushort[] { 100, 200, 300 }));
        var step = Enumerable.Repeat((ushort)124, 64).Concat(Enumerable.Repeat((ushort)869, 64)).ToArray();
        var filtered = ReferenceFilter.Process(step);
        Assert.Equal(170, filtered[64]); Assert.Equal(869, filtered[79]);
        Assert.Throws<ArgumentException>(() => ReferenceFilter.Process(new ushort[] { 4096 }));
    }

    [Fact]
    public void StatisticsUsesSampleDeviationAndExplicitInsufficientData()
    {
        var stats = MeasurementAnalysis.Stats(new double[] { 1, 2, 3 });
        Assert.Equal(2, stats.MeanV); Assert.Equal(1, stats.SampleStddevV);
        Assert.Equal("VALID", stats.Status);
        foreach (double[] values in new[] { Array.Empty<double>(), new double[] { 2 } })
        {
            stats = MeasurementAnalysis.Stats(values);
            Assert.Equal("INSUFFICIENT_DATA", stats.Status); Assert.Null(stats.SampleStddevV);
            Assert.DoesNotContain("NaN", JsonSerializer.Serialize(stats));
        }
    }

    [Fact]
    public void SummarySeparatesSettledWindowClippingAndBaseline()
    {
        ushort[] raw = [0, 4095, .. Enumerable.Repeat((ushort)124, 30)];
        var result = MeasurementAnalysis.Summarise(raw, ReferenceFilter.Process(raw), .1);
        Assert.Equal(32, result.Raw.Count); Assert.Equal(17, result.FilteredSettled.Count);
        Assert.Equal(15, result.DiscardedStartupSamples); Assert.Equal(2.0 / 32, result.ClippingRate);
        Assert.Equal(result.Raw.MeanV - .1, result.BaselineSubtractedV);
        Assert.Throws<ArgumentException>(() => MeasurementAnalysis.Summarise(raw, new ushort[1]));
    }

    [Fact]
    public void FitExcludesRailsAndNeedsThreeDistinctUnclippedLevels()
    {
        var fit = MeasurementAnalysis.FitLine(new FitPoint[] {
            new(0, .1), new(10, .7), new(20, 1.3), new(60, 3.3, true) });
        Assert.Equal(.060, fit.SlopeVPerUw, 10); Assert.Equal(.1, fit.InterceptV, 10);
        Assert.Equal(3, fit.Included); Assert.Equal(1, fit.Excluded);
        Assert.Throws<ArgumentException>(() => MeasurementAnalysis.FitLine(new FitPoint[] {
            new(0, .1), new(10, .7), new(60, 3.3, true) }));
        Assert.Throws<ArgumentException>(() => MeasurementAnalysis.FitLine(new FitPoint[] {
            new(0, .1), new(0, .1), new(10, .7) }));
    }

    [Fact]
    public void SavedNoiseMatchesIndependentPythonReference()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "optical", "noise-20uw.json")));
        var root = doc.RootElement;
        var raw = root.GetProperty("samples").EnumerateArray().Select(x => x.GetUInt16()).ToArray();
        var expected = root.GetProperty("reference_filtered").EnumerateArray().Select(x => x.GetUInt16()).ToArray();
        Assert.Equal(expected, ReferenceFilter.Process(raw));
        var summary = MeasurementAnalysis.Summarise(raw, expected);
        foreach (var (key, actual) in new[] { ("raw", summary.Raw), ("filtered_settled", summary.FilteredSettled) })
        {
            var reference = root.GetProperty("reference").GetProperty(key);
            Assert.InRange(Math.Abs(actual.MeanV!.Value - reference.GetProperty("mean_v").GetDouble()), 0, 1e-6);
            Assert.InRange(Math.Abs(actual.SampleStddevV!.Value - reference.GetProperty("sample_stddev_v").GetDouble()), 0, 1e-6);
        }
    }
}
