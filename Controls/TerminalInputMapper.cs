using Avalonia.Input;
using XKey = XTerm.Input.Key;
using XKeyModifiers = XTerm.Input.KeyModifiers;

namespace VeloShell.Controls;

public static class TerminalInputMapper
{
    public static bool TryMapToXTermKey(Key key, out XKey xtermKey)
    {
        xtermKey = key switch
        {
            Key.Enter => XKey.Enter,
            Key.Tab => XKey.Tab,
            Key.Back => XKey.Backspace,
            Key.Escape => XKey.Escape,
            Key.Space => XKey.Space,
            Key.Up => XKey.UpArrow,
            Key.Down => XKey.DownArrow,
            Key.Right => XKey.RightArrow,
            Key.Left => XKey.LeftArrow,
            Key.Home => XKey.Home,
            Key.End => XKey.End,
            Key.PageUp => XKey.PageUp,
            Key.PageDown => XKey.PageDown,
            Key.Insert => XKey.Insert,
            Key.Delete => XKey.Delete,
            Key.F1 => XKey.F1,
            Key.F2 => XKey.F2,
            Key.F3 => XKey.F3,
            Key.F4 => XKey.F4,
            Key.F5 => XKey.F5,
            Key.F6 => XKey.F6,
            Key.F7 => XKey.F7,
            Key.F8 => XKey.F8,
            Key.F9 => XKey.F9,
            Key.F10 => XKey.F10,
            Key.F11 => XKey.F11,
            Key.F12 => XKey.F12,
            _ => (XKey)(-1)
        };

        return (int)xtermKey != -1;
    }

    public static XKeyModifiers MapModifiers(KeyModifiers modifiers)
    {
        var result = XKeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            result |= XKeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Control))
            result |= XKeyModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            result |= XKeyModifiers.Alt;
        return result;
    }

    public static bool TryMapControlKey(Key key, KeyModifiers modifiers, out string sequence)
    {
        sequence = string.Empty;
        bool hasCtrl = modifiers.HasFlag(KeyModifiers.Control);

        if (!hasCtrl) return false;

        if (key >= Key.A && key <= Key.Z)
        {
            int code = (int)key - (int)Key.A + 1;
            sequence = ((char)code).ToString();
            return true;
        }

        switch (key)
        {
            case Key.OemOpenBrackets: // Ctrl+[ -> ESC
                sequence = "\x1b";
                return true;
            case Key.OemBackslash: // Ctrl+\ -> SIGQUIT
                sequence = "\x1c";
                return true;
            case Key.OemCloseBrackets: // Ctrl+]
                sequence = "\x1d";
                return true;
            default:
                return false;
        }
    }
}
