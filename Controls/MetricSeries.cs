using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace VeloShell.Controls;

/// <summary>
/// Fixed-capacity ring buffer of metric samples feeding a <see cref="Sparkline"/>.
/// Enumeration and indexing run oldest -> newest; once full, adding a sample
/// evicts the oldest. Raises a <see cref="NotifyCollectionChangedAction.Reset"/>
/// on every mutation so a bound sparkline redraws as samples stream in.
/// The collector and its cadence are untouched; this only retains a short window
/// of history the collector itself does not keep (see system-dashboard design).
/// </summary>
public sealed class MetricSeries : IReadOnlyList<double>, INotifyCollectionChanged
{
    private readonly double[] _buffer;
    private int _start; // index of the oldest sample
    private int _count;

    public MetricSeries(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        Capacity = capacity;
        _buffer = new double[capacity];
    }

    public int Capacity { get; }

    public int Count => _count;

    public double this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _buffer[(_start + index) % Capacity];
        }
    }

    /// <summary>Appends the newest sample, evicting the oldest once at capacity.</summary>
    public void Add(double value)
    {
        if (_count < Capacity)
        {
            _buffer[(_start + _count) % Capacity] = value;
            _count++;
        }
        else
        {
            _buffer[_start] = value;
            _start = (_start + 1) % Capacity;
        }

        CollectionChanged?.Invoke(this, ResetArgs);
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
        CollectionChanged?.Invoke(this, ResetArgs);
    }

    public IEnumerator<double> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return _buffer[(_start + i) % Capacity];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private static readonly NotifyCollectionChangedEventArgs ResetArgs =
        new(NotifyCollectionChangedAction.Reset);
}
