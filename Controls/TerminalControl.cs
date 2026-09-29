using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using XTerm;
using XTerm.Buffer;
using XTerm.Common;
using XTerm.Options;
using CursorStyle = XTerm.Common.CursorStyle;
using Key = XTerm.Input.Key;
using KeyModifiers = XTerm.Input.KeyModifiers;
using XTermSelectionMode = XTerm.Selection.SelectionMode;

namespace VeloShell.Controls;

public class TerminalResizeEventArgs : EventArgs
{
    public int Columns { get; }
    public int Rows { get; }
    public int Width { get; }
    public int Height { get; }

    public TerminalResizeEventArgs(int columns, int rows, int width, int height)
    {
        Columns = columns;
        Rows = rows;
        Width = width;
        Height = height;
    }
}

public class TerminalControl : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalControl, FontFamily>(
            nameof(FontFamily),
            new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalControl, double>(nameof(FontSize), 14.0);

    public static readonly StyledProperty<IBrush> TerminalBackgroundProperty =
        AvaloniaProperty.Register<TerminalControl, IBrush>(
            nameof(TerminalBackground),
            new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));

    public static readonly StyledProperty<IBrush> TerminalForegroundProperty =
        AvaloniaProperty.Register<TerminalControl, IBrush>(
            nameof(TerminalForeground),
            new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)));

    public static readonly StyledProperty<IBrush> SelectionBrushProperty =
        AvaloniaProperty.Register<TerminalControl, IBrush>(
            nameof(SelectionBrush),
            new SolidColorBrush(Color.FromArgb(0x66, 0x26, 0x4F, 0x78)));

    public static readonly StyledProperty<IBrush> CursorBrushProperty =
        AvaloniaProperty.Register<TerminalControl, IBrush>(
            nameof(CursorBrush),
            new SolidColorBrush(Color.FromRgb(0xAE, 0xAF, 0xAD)));

    public static readonly StyledProperty<CursorStyle> CursorStyleProperty =
        AvaloniaProperty.Register<TerminalControl, CursorStyle>(
            nameof(CursorStyle),
            CursorStyle.Block);

    public static readonly StyledProperty<bool> CursorBlinkProperty =
        AvaloniaProperty.Register<TerminalControl, bool>(nameof(CursorBlink), true);

    public static readonly DirectProperty<TerminalControl, int> ViewportYProperty =
        AvaloniaProperty.RegisterDirect<TerminalControl, int>(
            nameof(ViewportY),
            o => o.ViewportY,
            (o, v) => o.ViewportY = v);

    public static readonly DirectProperty<TerminalControl, int> TotalLinesProperty =
        AvaloniaProperty.RegisterDirect<TerminalControl, int>(
            nameof(TotalLines),
            o => o.TotalLines);

    public static readonly DirectProperty<TerminalControl, int> BaseYProperty =
        AvaloniaProperty.RegisterDirect<TerminalControl, int>(
            nameof(BaseY),
            o => o.BaseY);

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush TerminalBackground
    {
        get => GetValue(TerminalBackgroundProperty);
        set => SetValue(TerminalBackgroundProperty, value);
    }

    public IBrush TerminalForeground
    {
        get => GetValue(TerminalForegroundProperty);
        set => SetValue(TerminalForegroundProperty, value);
    }

    public IBrush SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public IBrush CursorBrush
    {
        get => GetValue(CursorBrushProperty);
        set => SetValue(CursorBrushProperty, value);
    }

    public CursorStyle CursorStyle
    {
        get => GetValue(CursorStyleProperty);
        set => SetValue(CursorStyleProperty, value);
    }

    public bool CursorBlink
    {
        get => GetValue(CursorBlinkProperty);
        set => SetValue(CursorBlinkProperty, value);
    }

    private int _viewportY;
    public int ViewportY
    {
        get => _viewportY;
        set
        {
            if (SetAndRaise(ViewportYProperty, ref _viewportY, value))
            {
                if (_terminal.Buffer != null)
                {
                    int targetLine = Math.Clamp(value, 0, Math.Max(0, _terminal.Buffer.BaseY));
                    _terminal.Buffer.ScrollToLine(targetLine);
                    RequestRender();
                }
            }
        }
    }

    public int TotalLines => _terminal.Buffer?.Lines.Length ?? 0;
    public int BaseY => _terminal.Buffer?.BaseY ?? 0;

    private readonly Terminal _terminal;
    private double _charWidth = 8.5;
    private double _charHeight = 18.0;
    private bool _renderScheduled;
    private bool _cursorVisible = true;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _resizeDebounceTimer;
    private readonly Dictionary<uint, SolidColorBrush> _brushCache = new();
    private bool _isDraggingSelection;

    public Terminal Terminal => _terminal;
    public int Columns => _terminal.Cols;
    public int Rows => _terminal.Rows;

    public event EventHandler<string>? UserInput;
    public event EventHandler<TerminalResizeEventArgs>? TerminalResized;
    public event EventHandler? ViewportMetricsChanged;

    static TerminalControl()
    {
        FocusableProperty.OverrideDefaultValue<TerminalControl>(true);
    }

    public TerminalControl()
    {
        var options = new TerminalOptions
        {
            Cols = 80,
            Rows = 24,
            Scrollback = 5000,
            CursorBlink = true,
            CursorStyle = CursorStyle.Block,
            TermName = "xterm-256color"
        };

        _terminal = new Terminal(options);
        _terminal.DataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UserInput?.Invoke(this, e.Data);
            }
        };

        _terminal.LineFed += (_, _) => OnTerminalOutputUpdated();
        _terminal.Scrolled += (_, _) => OnTerminalOutputUpdated();
        _terminal.BufferChanged += (_, _) => OnTerminalOutputUpdated();
        _terminal.Selection.SelectionChanged += () => RequestRender();

        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += (_, _) =>
        {
            if (CursorBlink)
            {
                _cursorVisible = !_cursorVisible;
                RequestRender();
            }
            else
            {
                _cursorVisible = true;
            }
        };
        _blinkTimer.Start();

        _resizeDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            PerformResize();
        };

        ClipToBounds = true;
    }

    private void OnTerminalOutputUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RaisePropertyChanged(TotalLinesProperty, 0, TotalLines);
            RaisePropertyChanged(BaseYProperty, 0, BaseY);
            if (_terminal.Buffer != null && _viewportY != _terminal.Buffer.YDisp)
            {
                _viewportY = _terminal.Buffer.YDisp;
                RaisePropertyChanged(ViewportYProperty, 0, _viewportY);
            }
            ViewportMetricsChanged?.Invoke(this, EventArgs.Empty);
            RequestRender();
        });
    }

    public void Write(string text)
    {
        _terminal.Write(text);
        OnTerminalOutputUpdated();
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        string text = Encoding.UTF8.GetString(data);
        _terminal.Write(text);
        OnTerminalOutputUpdated();
    }

    public void Clear()
    {
        _terminal.Clear();
        OnTerminalOutputUpdated();
    }

    public void Reset()
    {
        _terminal.Reset();
        OnTerminalOutputUpdated();
    }

    public void RequestRender()
    {
        if (_renderScheduled) return;
        _renderScheduled = true;

        Dispatcher.UIThread.Post(() =>
        {
            _renderScheduled = false;
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    private void MeasureCharacterDimensions()
    {
        var typeface = new Typeface(FontFamily);
        var formatted = new FormattedText(
            "M",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.White);

        _charWidth = Math.Max(1.0, formatted.Width);
        _charHeight = Math.Max(1.0, formatted.Height);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCharacterDimensions();
        double w = double.IsInfinity(availableSize.Width) ? _charWidth * 80 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? _charHeight * 24 : availableSize.Height;
        return new Size(w, h);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void PerformResize()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        MeasureCharacterDimensions();
        int newCols = Math.Clamp((int)(Bounds.Width / _charWidth), 1, 500);
        int newRows = Math.Clamp((int)(Bounds.Height / _charHeight), 1, 200);

        if (newCols != _terminal.Cols || newRows != _terminal.Rows)
        {
            _terminal.Resize(newCols, newRows);
            OnTerminalOutputUpdated();
            TerminalResized?.Invoke(this, new TerminalResizeEventArgs(newCols, newRows, (int)Bounds.Width, (int)Bounds.Height));
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 1. Draw Control Background
        context.DrawRectangle(TerminalBackground, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

        var buffer = _terminal.Buffer;
        if (buffer == null || _charWidth <= 0 || _charHeight <= 0) return;

        int yDisp = buffer.YDisp;
        int screenRows = _terminal.Rows;
        int screenCols = _terminal.Cols;

        var typeface = new Typeface(FontFamily);

        // 2. Batch Render Visible Lines
        for (int r = 0; r < screenRows; r++)
        {
            int bufferLineIndex = yDisp + r;
            BufferLine? line = bufferLineIndex < buffer.Lines.Length ? buffer.Lines[bufferLineIndex] : null;

            var spans = TerminalSpanBatcher.BatchLine(
                line,
                screenCols,
                _terminal.Colors,
                bufferLineIndex,
                _terminal.Selection);

            double rowY = r * _charHeight;

            foreach (var span in spans)
            {
                double spanX = span.StartCol * _charWidth;
                double spanW = span.Length * _charWidth;

                // Draw background if customized or selected
                if (span.IsSelected)
                {
                    context.DrawRectangle(SelectionBrush, null, new Rect(spanX, rowY, spanW, _charHeight));
                }
                else if (span.BgColor != 0xFF1E1E1E && span.BgColor != 0xFF000000)
                {
                    var bgBrush = GetOrCreateBrush(span.BgColor);
                    context.DrawRectangle(bgBrush, null, new Rect(spanX, rowY, spanW, _charHeight));
                }

                // Draw text if non-empty
                if (!string.IsNullOrWhiteSpace(span.Text))
                {
                    var spanTypeface = (span.IsBold || span.IsItalic)
                        ? new Typeface(
                            FontFamily,
                            span.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                            span.IsBold ? FontWeight.Bold : FontWeight.Normal)
                        : typeface;

                    var fgBrush = GetOrCreateBrush(span.FgColor);
                    var formattedText = new FormattedText(
                        span.Text,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        spanTypeface,
                        FontSize,
                        fgBrush);

                    context.DrawText(formattedText, new Point(spanX, rowY));

                    if (span.IsUnderline)
                    {
                        var pen = new Pen(fgBrush, 1.0);
                        context.DrawLine(pen, new Point(spanX, rowY + _charHeight - 1), new Point(spanX + spanW, rowY + _charHeight - 1));
                    }
                }
            }
        }

        // 3. Draw Cursor
        if (IsFocused && _cursorVisible && _terminal.CursorVisible)
        {
            int cursorScreenY = buffer.Y - (buffer.YDisp - buffer.BaseY);
            if (cursorScreenY >= 0 && cursorScreenY < screenRows && buffer.X >= 0 && buffer.X < screenCols)
            {
                double cursorX = buffer.X * _charWidth;
                double cursorY = cursorScreenY * _charHeight;

                switch (CursorStyle)
                {
                    case CursorStyle.Bar:
                        context.DrawRectangle(CursorBrush, null, new Rect(cursorX, cursorY, 2, _charHeight));
                        break;
                    case CursorStyle.Underline:
                        context.DrawRectangle(CursorBrush, null, new Rect(cursorX, cursorY + _charHeight - 2, _charWidth, 2));
                        break;
                    case CursorStyle.Block:
                    default:
                        context.DrawRectangle(CursorBrush, null, new Rect(cursorX, cursorY, _charWidth, _charHeight));
                        break;
                }
            }
        }
    }

    private SolidColorBrush GetOrCreateBrush(uint color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            byte a = (byte)((color >> 24) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            if (a == 0) a = 0xFF;

            brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            _brushCache[color] = brush;
        }
        return brush;
    }

    #region Input Bridging

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        // Clipboard Shortcuts: Ctrl+Shift+C (Copy), Ctrl+Shift+V (Paste)
        bool hasCtrl = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        bool hasShift = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);

        if (hasCtrl && hasShift && e.Key == Avalonia.Input.Key.C)
        {
            _ = CopySelectionToClipboardAsync();
            e.Handled = true;
            return;
        }

        if (hasCtrl && hasShift && e.Key == Avalonia.Input.Key.V)
        {
            _ = PasteFromClipboardAsync();
            e.Handled = true;
            return;
        }

        // Viewport PageUp/PageDown when not in alt screen
        if (!_terminal.IsAlternateBufferActive)
        {
            if (e.Key == Avalonia.Input.Key.PageUp && hasShift)
            {
                ScrollLines(-Rows);
                e.Handled = true;
                return;
            }
            if (e.Key == Avalonia.Input.Key.PageDown && hasShift)
            {
                ScrollLines(Rows);
                e.Handled = true;
                return;
            }
        }

        // Check for Control characters (Ctrl+C, Ctrl+D, Ctrl+Z, etc.)
        if (TerminalInputMapper.TryMapControlKey(e.Key, e.KeyModifiers, out string ctrlSeq))
        {
            SendUserInput(ctrlSeq);
            e.Handled = true;
            return;
        }

        // Map Avalonia Key to XTerm Key
        if (TerminalInputMapper.TryMapToXTermKey(e.Key, out var xtermKey))
        {
            var xtermModifiers = TerminalInputMapper.MapModifiers(e.KeyModifiers);
            string seq = _terminal.GenerateKeyInput(xtermKey, xtermModifiers);
            if (!string.IsNullOrEmpty(seq))
            {
                SendUserInput(seq);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrEmpty(e.Text)) return;

        SendUserInput(e.Text);
        e.Handled = true;
    }

    private void SendUserInput(string input)
    {
        // Jump viewport to bottom on user input
        if (_terminal.Buffer != null && _terminal.Buffer.YDisp != _terminal.Buffer.BaseY)
        {
            _terminal.Buffer.ScrollToBottom();
            OnTerminalOutputUpdated();
        }

        UserInput?.Invoke(this, input);
    }

    #endregion

    #region Scrolling & Selection

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        int delta = -(int)e.Delta.Y * 3;
        ScrollLines(delta);
        e.Handled = true;
    }

    public void ScrollLines(int delta)
    {
        if (_terminal.Buffer == null) return;
        _terminal.Buffer.ScrollLines(delta);
        OnTerminalOutputUpdated();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);
        int col = Math.Clamp((int)(pos.X / _charWidth), 0, _terminal.Cols - 1);
        int row = Math.Clamp((int)(pos.Y / _charHeight) + (_terminal.Buffer?.YDisp ?? 0), 0, Math.Max(0, TotalLines - 1));

        if (e.ClickCount == 1)
        {
            _terminal.Selection.ClearSelection();
            _terminal.Selection.StartSelection(col, row, XTermSelectionMode.Normal);
            _isDraggingSelection = true;
        }
        else if (e.ClickCount == 2)
        {
            _terminal.Selection.StartSelection(col, row, XTermSelectionMode.Word);
            _isDraggingSelection = false;
        }
        else if (e.ClickCount >= 3)
        {
            _terminal.Selection.StartSelection(col, row, XTermSelectionMode.Line);
            _isDraggingSelection = false;
        }

        RequestRender();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDraggingSelection) return;

        var pos = e.GetPosition(this);
        int col = Math.Clamp((int)(pos.X / _charWidth), 0, _terminal.Cols - 1);
        int row = Math.Clamp((int)(pos.Y / _charHeight) + (_terminal.Buffer?.YDisp ?? 0), 0, Math.Max(0, TotalLines - 1));

        _terminal.Selection.UpdateSelection(col, row);
        RequestRender();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDraggingSelection)
        {
            _isDraggingSelection = false;
            _terminal.Selection.EndSelection();
            RequestRender();
            e.Handled = true;
        }
    }

    public async Task CopySelectionToClipboardAsync()
    {
        string text = _terminal.Selection.GetSelectionText();
        if (!string.IsNullOrEmpty(text))
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
    }

    public async Task PasteFromClipboardAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            string? text = await topLevel.Clipboard.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                SendUserInput(text);
            }
        }
    }

    #endregion
}
