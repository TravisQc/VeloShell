using System;
using System.Collections.Generic;

namespace VeloShell.Controls;

/// <summary>
/// Pure scaling helpers for <see cref="Sparkline"/>. Kept free of Avalonia types
/// so the normalization / adaptive-scaling logic is unit-testable in isolation.
/// </summary>
public static class SparklineGeometry
{
    /// <summary>
    /// Resolves the vertical scale ceiling. A positive, finite <paramref name="configuredMax"/>
    /// is used as a fixed scale (e.g. 100 for a percentage); otherwise the scale adapts to the
    /// largest sample across every supplied series (the window peak), as the network graph does.
    /// Returns 0 when there is nothing to scale against.
    /// </summary>
    public static double ResolveMax(double configuredMax, params IReadOnlyList<double>?[] series)
    {
        if (configuredMax > 0 && !double.IsNaN(configuredMax) && !double.IsInfinity(configuredMax))
            return configuredMax;

        double max = 0;
        if (series != null)
        {
            foreach (var s in series)
            {
                if (s == null) continue;
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i] > max) max = s[i];
                }
            }
        }

        return max;
    }

    /// <summary>
    /// Maps samples to the [0,1] range against <paramref name="max"/>, clamping out-of-range
    /// values. An empty input yields an empty result; a non-positive <paramref name="max"/>
    /// (empty / all-zero window) yields all zeros rather than dividing by zero.
    /// </summary>
    public static double[] Normalize(IReadOnlyList<double>? samples, double max)
    {
        if (samples == null || samples.Count == 0)
            return Array.Empty<double>();

        var result = new double[samples.Count];
        if (max <= 0)
            return result; // all zeros

        for (int i = 0; i < samples.Count; i++)
        {
            double v = samples[i] / max;
            result[i] = v < 0 ? 0 : v > 1 ? 1 : v;
        }

        return result;
    }
}
