using VeloShell.Controls;
using Xunit;

namespace VeloShell.Tests;

public class SparklineGeometryTests
{
    [Fact]
    public void ResolveMax_UsesConfiguredMax_WhenPositiveFinite()
    {
        double max = SparklineGeometry.ResolveMax(100, new double[] { 1, 2, 3 });

        Assert.Equal(100, max);
    }

    [Fact]
    public void ResolveMax_AutoScalesToWindowPeak_WhenUnset()
    {
        // NaN => adaptive: scale to the largest sample (network graph behavior).
        double max = SparklineGeometry.ResolveMax(double.NaN, new double[] { 1, 2, 4 });

        Assert.Equal(4, max);
    }

    [Fact]
    public void ResolveMax_CombinesMultipleSeries_ForSharedScale()
    {
        // Network down / up overlay on one shared adaptive scale.
        double max = SparklineGeometry.ResolveMax(
            double.NaN,
            new double[] { 1, 2 },
            new double[] { 3, 9 });

        Assert.Equal(9, max);
    }

    [Fact]
    public void ResolveMax_Empty_ReturnsZero()
    {
        Assert.Equal(0, SparklineGeometry.ResolveMax(double.NaN));
        Assert.Equal(0, SparklineGeometry.ResolveMax(double.NaN, new double[0]));
    }

    [Fact]
    public void Normalize_FixedScale_MapsToUnitRange()
    {
        var result = SparklineGeometry.Normalize(new double[] { 0, 50, 100 }, 100);

        Assert.Equal(new double[] { 0.0, 0.5, 1.0 }, result);
    }

    [Fact]
    public void Normalize_AdaptiveScale_MapsAgainstWindowPeak()
    {
        var samples = new double[] { 2, 4, 8 };
        double max = SparklineGeometry.ResolveMax(double.NaN, samples);

        var result = SparklineGeometry.Normalize(samples, max);

        Assert.Equal(new double[] { 0.25, 0.5, 1.0 }, result);
    }

    [Fact]
    public void Normalize_Empty_ReturnsEmpty()
    {
        Assert.Empty(SparklineGeometry.Normalize(new double[0], 100));
        Assert.Empty(SparklineGeometry.Normalize(null, 100));
    }

    [Fact]
    public void Normalize_NonPositiveMax_ReturnsZeros_NoDivideByZero()
    {
        var result = SparklineGeometry.Normalize(new double[] { 5, 5 }, 0);

        Assert.Equal(new double[] { 0.0, 0.0 }, result);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeSamples()
    {
        var result = SparklineGeometry.Normalize(new double[] { 150, -10 }, 100);

        Assert.Equal(new double[] { 1.0, 0.0 }, result);
    }
}
