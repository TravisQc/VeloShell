using System;
using System.Collections.Specialized;
using System.Linq;
using VeloShell.Controls;
using Xunit;

namespace VeloShell.Tests;

public class MetricSeriesTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricSeries(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetricSeries(-1));
    }

    [Fact]
    public void Empty_HasNoItems()
    {
        var series = new MetricSeries(5);

        Assert.Empty(series);
    }

    [Fact]
    public void Add_AppendsNewestLast_PreservingOrder()
    {
        var series = new MetricSeries(5);

        series.Add(1);
        series.Add(2);
        series.Add(3);

        Assert.Equal(3, series.Count);
        Assert.Equal(1, series[0]);
        Assert.Equal(3, series[2]);
        Assert.Equal(new double[] { 1, 2, 3 }, series.ToArray());
    }

    [Fact]
    public void Add_BeyondCapacity_EvictsOldest_KeepingWindow()
    {
        var series = new MetricSeries(3);

        for (int i = 1; i <= 5; i++)
            series.Add(i);

        // Window holds only the last 3 samples, oldest -> newest.
        Assert.Equal(3, series.Count);
        Assert.Equal(new double[] { 3, 4, 5 }, series.ToArray());
        Assert.Equal(3, series[0]);
        Assert.Equal(5, series[2]);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var series = new MetricSeries(3);
        series.Add(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => series[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => series[-1]);
    }

    [Fact]
    public void Add_RaisesCollectionChanged()
    {
        var series = new MetricSeries(3);
        int raised = 0;
        series.CollectionChanged += (_, e) =>
        {
            raised++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        series.Add(42);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Clear_EmptiesSeries()
    {
        var series = new MetricSeries(3);
        series.Add(1);
        series.Add(2);

        series.Clear();

        Assert.Empty(series);
    }
}
