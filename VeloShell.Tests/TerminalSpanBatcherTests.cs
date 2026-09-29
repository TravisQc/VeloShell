using System.Collections.Generic;
using VeloShell.Controls;
using XTerm;
using XTerm.Options;
using XTerm.Selection;
using Xunit;

namespace VeloShell.Tests;

public class TerminalSpanBatcherTests
{
    [Fact]
    public void BatchLine_WhenLineNull_ReturnsFullWidthDefaultSpan()
    {
        var spans = TerminalSpanBatcher.BatchLine(null, 80, null);

        Assert.Single(spans);
        Assert.Equal(0, spans[0].StartCol);
        Assert.Equal(80, spans[0].Length);
        Assert.Equal(80, spans[0].Text.Length);
    }

    [Fact]
    public void BatchLine_WhenSingleStyleText_AggregatesContiguousCells()
    {
        var terminal = new Terminal(new TerminalOptions { Cols = 80, Rows = 24 });
        terminal.Write("Hello World");

        var line = terminal.Buffer.Lines[0];
        var spans = TerminalSpanBatcher.BatchLine(line, 80, terminal.Colors);

        // "Hello World" is 11 chars with default style; remaining 69 are blank spaces with default style
        // Since they share the same default style, they are aggregated into 1 span!
        Assert.Single(spans);
        Assert.Equal(80, spans[0].Length);
        Assert.StartsWith("Hello World", spans[0].Text);
    }

    [Fact]
    public void BatchLine_WhenColorChanges_SplitsIntoDistinctSpans()
    {
        var terminal = new Terminal(new TerminalOptions { Cols = 80, Rows = 24 });
        terminal.Write("\x1b[31mRED\x1b[32mGREEN\x1b[0mPLAIN");

        var line = terminal.Buffer.Lines[0];
        var spans = TerminalSpanBatcher.BatchLine(line, 80, terminal.Colors);

        // Expect at least 3 spans: RED (start 0, len 3), GREEN (start 3, len 5), PLAIN (start 8, len 72)
        Assert.True(spans.Count >= 3);

        Assert.Equal(0, spans[0].StartCol);
        Assert.Equal(3, spans[0].Length);
        Assert.Equal("RED", spans[0].Text);

        Assert.Equal(3, spans[1].StartCol);
        Assert.Equal(5, spans[1].Length);
        Assert.Equal("GREEN", spans[1].Text);

        Assert.NotEqual(spans[0].FgColor, spans[1].FgColor);

        Assert.Equal(8, spans[2].StartCol);
        Assert.StartsWith("PLAIN", spans[2].Text);
    }

    [Fact]
    public void BatchLine_WhenStyleFlagsChange_SplitsSpansWithAttributes()
    {
        var terminal = new Terminal(new TerminalOptions { Cols = 80, Rows = 24 });
        terminal.Write("\x1b[1mBOLD\x1b[22m\x1b[4mUNDER\x1b[24m");

        var line = terminal.Buffer.Lines[0];
        var spans = TerminalSpanBatcher.BatchLine(line, 80, terminal.Colors);

        Assert.True(spans.Count >= 2);
        Assert.Equal("BOLD", spans[0].Text);
        Assert.True(spans[0].IsBold);
        Assert.False(spans[0].IsUnderline);

        Assert.Equal("UNDER", spans[1].Text);
        Assert.False(spans[1].IsBold);
        Assert.True(spans[1].IsUnderline);
    }

    [Fact]
    public void BatchLine_WithSelection_MarksSelectedCells()
    {
        var terminal = new Terminal(new TerminalOptions { Cols = 80, Rows = 24 });
        terminal.Write("SELECTME REST");

        // Select columns 0 to 7 on line 0
        terminal.Selection.StartSelection(0, 0, SelectionMode.Normal);
        terminal.Selection.UpdateSelection(7, 0);

        var line = terminal.Buffer.Lines[0];
        var spans = TerminalSpanBatcher.BatchLine(line, 80, terminal.Colors, lineIndex: 0, selection: terminal.Selection);

        Assert.True(spans.Count >= 2);
        Assert.True(spans[0].IsSelected);
        Assert.False(spans[1].IsSelected);
    }
}
