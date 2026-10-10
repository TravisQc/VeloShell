using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VeloShell.Converters;

/// <summary>Value converters used by the window shell.</summary>
public static class AppConverters
{
    /// <summary>
    /// True when a string carries meaningful data — not null, not empty, and not the
    /// "-" placeholder the dashboard uses before metrics arrive. Used to hide
    /// status-bar fields whose data is not yet available (graceful degradation).
    /// </summary>
    public static readonly IValueConverter MeaningfulText = new FuncValueConverter<string?, bool>(
        s => !string.IsNullOrWhiteSpace(s) && s != "-");
}
