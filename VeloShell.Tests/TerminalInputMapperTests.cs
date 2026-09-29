using Avalonia.Input;
using VeloShell.Controls;
using XTerm;
using XTerm.Options;
using Xunit;
using XKey = XTerm.Input.Key;
using XKeyModifiers = XTerm.Input.KeyModifiers;

namespace VeloShell.Tests;

public class TerminalInputMapperTests
{
    [Theory]
    [InlineData(Key.Up, XKey.UpArrow)]
    [InlineData(Key.Down, XKey.DownArrow)]
    [InlineData(Key.Left, XKey.LeftArrow)]
    [InlineData(Key.Right, XKey.RightArrow)]
    [InlineData(Key.Enter, XKey.Enter)]
    [InlineData(Key.Tab, XKey.Tab)]
    [InlineData(Key.Back, XKey.Backspace)]
    [InlineData(Key.Escape, XKey.Escape)]
    [InlineData(Key.Home, XKey.Home)]
    [InlineData(Key.End, XKey.End)]
    [InlineData(Key.PageUp, XKey.PageUp)]
    [InlineData(Key.PageDown, XKey.PageDown)]
    [InlineData(Key.Delete, XKey.Delete)]
    [InlineData(Key.F1, XKey.F1)]
    [InlineData(Key.F12, XKey.F12)]
    public void TryMapToXTermKey_MapsStandardKeysCorrectly(Key avaloniaKey, XKey expectedXTermKey)
    {
        bool mapped = TerminalInputMapper.TryMapToXTermKey(avaloniaKey, out var actualKey);
        Assert.True(mapped);
        Assert.Equal(expectedXTermKey, actualKey);
    }

    [Fact]
    public void MapModifiers_TranslatesFlagsCorrectly()
    {
        var mods = KeyModifiers.Control | KeyModifiers.Shift;
        var xtermMods = TerminalInputMapper.MapModifiers(mods);

        Assert.True(xtermMods.HasFlag(XKeyModifiers.Control));
        Assert.True(xtermMods.HasFlag(XKeyModifiers.Shift));
        Assert.False(xtermMods.HasFlag(XKeyModifiers.Alt));
    }

    [Theory]
    [InlineData(Key.C, "\x03")] // Ctrl+C (SIGINT)
    [InlineData(Key.D, "\x04")] // Ctrl+D (EOF)
    [InlineData(Key.Z, "\x1a")] // Ctrl+Z (SIGTSTP)
    [InlineData(Key.A, "\x01")] // Ctrl+A (Line start)
    [InlineData(Key.E, "\x05")] // Ctrl+E (Line end)
    [InlineData(Key.L, "\x0c")] // Ctrl+L (Clear screen)
    [InlineData(Key.OemOpenBrackets, "\x1b")] // Ctrl+[ (ESC)
    public void TryMapControlKey_GeneratesCorrectControlCharacters(Key key, string expectedSeq)
    {
        bool result = TerminalInputMapper.TryMapControlKey(key, KeyModifiers.Control, out string seq);
        Assert.True(result);
        Assert.Equal(expectedSeq, seq);
    }

    [Fact]
    public void GenerateKeyInput_ProducesExpectedVTSequence()
    {
        var terminal = new Terminal(new TerminalOptions { Cols = 80, Rows = 24 });

        // Up arrow should generate \x1b[A
        string upSeq = terminal.GenerateKeyInput(XKey.UpArrow, XKeyModifiers.None);
        Assert.Equal("\x1b[A", upSeq);

        // Down arrow should generate \x1b[B
        string downSeq = terminal.GenerateKeyInput(XKey.DownArrow, XKeyModifiers.None);
        Assert.Equal("\x1b[B", downSeq);
    }
}
