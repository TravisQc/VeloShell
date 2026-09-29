using VeloShell.Controls;
using XTerm;
using XTerm.Options;
using XTerm.Selection;
using Xunit;

namespace VeloShell.Tests;

public class TerminalSelectionAndScrollTests
{
    [Fact]
    public void Scroll_WhenMultipleLinesWritten_UpdatesYDispAndBaseY()
    {
        var terminal = new Terminal(new TerminalOptions
        {
            Cols = 80,
            Rows = 10,
            Scrollback = 100
        });

        for (int i = 0; i < 30; i++)
        {
            terminal.WriteLine($"Line {i}");
        }

        Assert.True(terminal.Buffer.BaseY > 0);
        Assert.Equal(terminal.Buffer.BaseY, terminal.Buffer.YDisp);

        // Scroll up by 5 lines
        terminal.Buffer.ScrollLines(-5);
        Assert.Equal(terminal.Buffer.BaseY - 5, terminal.Buffer.YDisp);

        // Scroll to top
        terminal.Buffer.ScrollToTop();
        Assert.Equal(0, terminal.Buffer.YDisp);

        // Scroll to bottom
        terminal.Buffer.ScrollToBottom();
        Assert.Equal(terminal.Buffer.BaseY, terminal.Buffer.YDisp);
    }

    [Fact]
    public void Selection_NormalMode_ExtractsSelectedRange()
    {
        var terminal = new Terminal(new TerminalOptions
        {
            Cols = 80,
            Rows = 10
        });

        terminal.Write("HELLO WORLD");

        // Select columns 0 to 4 ("HELLO")
        terminal.Selection.StartSelection(0, 0, SelectionMode.Normal);
        terminal.Selection.UpdateSelection(4, 0);
        terminal.Selection.EndSelection();

        string text = terminal.Selection.GetSelectionText();
        Assert.Equal("HELLO", text);
    }

    [Fact]
    public void Selection_WordMode_SelectsWholeWord()
    {
        var terminal = new Terminal(new TerminalOptions
        {
            Cols = 80,
            Rows = 10
        });

        terminal.Write("first_identifier second_identifier");

        // Click on index 3 inside "first_identifier"
        terminal.Selection.StartSelection(3, 0, SelectionMode.Word);
        terminal.Selection.EndSelection();

        string text = terminal.Selection.GetSelectionText();
        Assert.Equal("first_identifier", text);
    }

    [Fact]
    public void Selection_LineMode_SelectsEntireLine()
    {
        var terminal = new Terminal(new TerminalOptions
        {
            Cols = 80,
            Rows = 10
        });

        terminal.Write("This is a complete line.");

        terminal.Selection.StartSelection(5, 0, SelectionMode.Line);
        terminal.Selection.EndSelection();

        string text = terminal.Selection.GetSelectionText();
        Assert.Equal("This is a complete line.", text.TrimEnd());
    }

    [Fact]
    public void Selection_ClearSelection_ClearsState()
    {
        var terminal = new Terminal(new TerminalOptions
        {
            Cols = 80,
            Rows = 10
        });

        terminal.Write("ABCDEF");
        terminal.Selection.StartSelection(0, 0, SelectionMode.Normal);
        terminal.Selection.UpdateSelection(3, 0);

        Assert.True(terminal.Selection.HasSelection);

        terminal.Selection.ClearSelection();
        Assert.False(terminal.Selection.HasSelection);
        Assert.Empty(terminal.Selection.GetSelectionText());
    }
}
