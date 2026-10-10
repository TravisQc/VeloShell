using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VeloShell.Controls;

/// <summary>
/// Lightweight self-drawn vector sparkline: maps a rolling sample window to a
/// smoothed (Catmull-Rom) polyline. CPU / RAM pass a fixed <see cref="Maximum"/>
/// of 100; the network graph leaves it unset so the scale adapts to the window
/// peak. A second, optional series overlays on the same adaptive scale (network
/// down / up). Strokes and fills come from visual-design-system brush tokens via
/// bindings; the control draws only when its samples change — no per-frame bitmap.
/// </summary>
public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Sparkline, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IEnumerable?> ItemsSource2Property =
        AvaloniaProperty.Register<Sparkline, IEnumerable?>(nameof(ItemsSource2));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> Stroke2Property =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke2));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(StrokeThickness), 1.5);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(Maximum), double.NaN);

    private const double FillOpacity = 0.16;

    static Sparkline()
    {
        AffectsRender<Sparkline>(
            StrokeProperty, Stroke2Property, FillProperty, StrokeThicknessProperty, MaximumProperty);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IEnumerable? ItemsSource2
    {
        get => GetValue(ItemsSource2Property);
        set => SetValue(ItemsSource2Property, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Stroke2
    {
        get => GetValue(Stroke2Property);
        set => SetValue(Stroke2Property, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
            Resubscribe(change.GetOldValue<IEnumerable?>(), change.GetNewValue<IEnumerable?>());
        else if (change.Property == ItemsSource2Property)
            Resubscribe(change.GetOldValue<IEnumerable?>(), change.GetNewValue<IEnumerable?>());
    }

    private void Resubscribe(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldIncc)
            oldIncc.CollectionChanged -= OnSamplesChanged;
        if (newValue is INotifyCollectionChanged newIncc)
            newIncc.CollectionChanged += OnSamplesChanged;
        InvalidateVisual();
    }

    private void OnSamplesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var primary = Snapshot(ItemsSource);
        var secondary = Snapshot(ItemsSource2);

        double max = SparklineGeometry.ResolveMax(Maximum, primary, secondary);
        double pad = StrokeThickness;

        if (Fill != null && primary.Count >= 1)
        {
            var area = BuildGeometry(primary, max, w, h, pad, fill: true);
            if (area != null)
            {
                using (context.PushOpacity(FillOpacity))
                    context.DrawGeometry(Fill, null, area);
            }
        }

        DrawLine(context, primary, max, w, h, pad, Stroke);
        DrawLine(context, secondary, max, w, h, pad, Stroke2);
    }

    private void DrawLine(DrawingContext context, IReadOnlyList<double> samples, double max,
        double w, double h, double pad, IBrush? brush)
    {
        if (brush == null || samples.Count == 0)
            return;

        var geo = BuildGeometry(samples, max, w, h, pad, fill: false);
        if (geo == null)
            return;

        var pen = new Pen(brush, StrokeThickness) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
        context.DrawGeometry(null, pen, geo);
    }

    private static StreamGeometry? BuildGeometry(IReadOnlyList<double> samples, double max,
        double w, double h, double pad, bool fill)
    {
        int n = samples.Count;
        if (n == 0)
            return null;

        var norm = SparklineGeometry.Normalize(samples, max);
        double usableH = h - 2 * pad;
        if (usableH < 0) usableH = 0;
        double baseline = h - pad;

        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double x = n == 1 ? w : (double)i / (n - 1) * w;
            double y = pad + (1 - norm[i]) * usableH;
            pts[i] = new Point(x, y);
        }

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        if (n == 1)
        {
            // A single sample draws as a flat line across the width at its level.
            if (fill)
            {
                ctx.BeginFigure(new Point(0, baseline), true);
                ctx.LineTo(new Point(0, pts[0].Y));
                ctx.LineTo(new Point(w, pts[0].Y));
                ctx.LineTo(new Point(w, baseline));
                ctx.EndFigure(true);
            }
            else
            {
                ctx.BeginFigure(new Point(0, pts[0].Y), false);
                ctx.LineTo(new Point(w, pts[0].Y));
                ctx.EndFigure(false);
            }

            return geometry;
        }

        if (fill)
        {
            ctx.BeginFigure(new Point(pts[0].X, baseline), true);
            ctx.LineTo(pts[0]);
        }
        else
        {
            ctx.BeginFigure(pts[0], false);
        }

        for (int i = 0; i < n - 1; i++)
        {
            var p0 = pts[i == 0 ? 0 : i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[i + 2 >= n ? n - 1 : i + 2];

            var cp1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var cp2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);
            ctx.CubicBezierTo(cp1, cp2, p2);
        }

        if (fill)
        {
            ctx.LineTo(new Point(pts[n - 1].X, baseline));
            ctx.EndFigure(true);
        }
        else
        {
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static IReadOnlyList<double> Snapshot(IEnumerable? source)
    {
        switch (source)
        {
            case null:
                return System.Array.Empty<double>();
            case IReadOnlyList<double> list:
                return list;
            default:
                var buffer = new List<double>();
                foreach (var item in source)
                {
                    if (item is double d)
                        buffer.Add(d);
                    else if (item is System.IConvertible)
                        buffer.Add(System.Convert.ToDouble(item));
                }
                return buffer;
        }
    }
}
