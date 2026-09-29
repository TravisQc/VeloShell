using System;
using System.Collections.Generic;
using System.Text;
using XTerm.Buffer;
using XTerm.Common;
using XTerm.Selection;

namespace VeloShell.Controls;

/// <summary>
/// Represents a contiguous sequence of characters sharing identical visual styles.
/// </summary>
public record struct TextRenderSpan(
    int StartCol,
    int Length,
    string Text,
    uint FgColor,
    uint BgColor,
    bool IsBold,
    bool IsItalic,
    bool IsUnderline,
    bool IsSelected);

/// <summary>
/// High-performance span batcher that groups adjacent buffer cells with identical visual attributes
/// to minimize text rendering calls in Avalonia.
/// </summary>
public static class TerminalSpanBatcher
{
    public static List<TextRenderSpan> BatchLine(
        BufferLine? line,
        int totalCols,
        ColorPalette? palette,
        int lineIndex = -1,
        SelectionManager? selection = null,
        uint defaultFg = 0xFFD4D4D4,
        uint defaultBg = 0xFF1E1E1E)
    {
        var spans = new List<TextRenderSpan>();
        if (totalCols <= 0) return spans;

        if (line == null)
        {
            spans.Add(new TextRenderSpan(
                StartCol: 0,
                Length: totalCols,
                Text: new string(' ', totalCols),
                FgColor: defaultFg,
                BgColor: defaultBg,
                IsBold: false,
                IsItalic: false,
                IsUnderline: false,
                IsSelected: false));
            return spans;
        }

        int startCol = 0;
        var textBuilder = new StringBuilder();
        uint currentFg = defaultFg;
        uint currentBg = defaultBg;
        bool currentBold = false;
        bool currentItalic = false;
        bool currentUnderline = false;
        bool currentSelected = false;
        bool inSpan = false;

        int colsToProcess = Math.Min(totalCols, line.Length);

        for (int col = 0; col < colsToProcess; col++)
        {
            var cell = line[col];

            // If width is 0, it's the second half of a wide (CJK) character; skip separate cell
            if (cell.Width == 0 && col > 0)
            {
                continue;
            }

            char ch = cell.CodePoint > 0 ? (char)cell.CodePoint : ' ';
            bool isSelected = selection != null && selection.HasSelection && lineIndex >= 0 && selection.IsCellSelected(col, lineIndex);

            ResolveCellColors(cell, palette, defaultFg, defaultBg, out uint fg, out uint bg);
            bool bold = cell.Attributes.IsBold();
            bool italic = cell.Attributes.IsItalic();
            bool underline = cell.Attributes.IsUnderline();

            if (!inSpan)
            {
                startCol = col;
                currentFg = fg;
                currentBg = bg;
                currentBold = bold;
                currentItalic = italic;
                currentUnderline = underline;
                currentSelected = isSelected;
                textBuilder.Append(ch);
                inSpan = true;
            }
            else
            {
                bool sameStyle = (fg == currentFg) &&
                                 (bg == currentBg) &&
                                 (bold == currentBold) &&
                                 (italic == currentItalic) &&
                                 (underline == currentUnderline) &&
                                 (isSelected == currentSelected);

                if (sameStyle)
                {
                    textBuilder.Append(ch);
                }
                else
                {
                    spans.Add(new TextRenderSpan(
                        StartCol: startCol,
                        Length: col - startCol,
                        Text: textBuilder.ToString(),
                        FgColor: currentFg,
                        BgColor: currentBg,
                        IsBold: currentBold,
                        IsItalic: currentItalic,
                        IsUnderline: currentUnderline,
                        IsSelected: currentSelected));

                    startCol = col;
                    currentFg = fg;
                    currentBg = bg;
                    currentBold = bold;
                    currentItalic = italic;
                    currentUnderline = underline;
                    currentSelected = isSelected;
                    textBuilder.Clear();
                    textBuilder.Append(ch);
                }
            }
        }

        if (inSpan && textBuilder.Length > 0)
        {
            spans.Add(new TextRenderSpan(
                StartCol: startCol,
                Length: colsToProcess - startCol,
                Text: textBuilder.ToString(),
                FgColor: currentFg,
                BgColor: currentBg,
                IsBold: currentBold,
                IsItalic: currentItalic,
                IsUnderline: currentUnderline,
                IsSelected: currentSelected));
        }

        // Fill remaining columns if line is shorter than totalCols
        if (colsToProcess < totalCols)
        {
            spans.Add(new TextRenderSpan(
                StartCol: colsToProcess,
                Length: totalCols - colsToProcess,
                Text: new string(' ', totalCols - colsToProcess),
                FgColor: defaultFg,
                BgColor: defaultBg,
                IsBold: false,
                IsItalic: false,
                IsUnderline: false,
                IsSelected: false));
        }

        return spans;
    }

    private static void ResolveCellColors(
        BufferCell cell,
        ColorPalette? palette,
        uint defaultFg,
        uint defaultBg,
        out uint fg,
        out uint bg)
    {
        int fgCode = cell.Attributes.GetFgColor();
        int fgMode = cell.Attributes.GetFgColorMode();

        if (fgMode == 1) // TrueColor RGB
        {
            fg = 0xFF000000u | (uint)(fgCode & 0xFFFFFF);
        }
        else if (palette != null && fgCode >= 0 && fgCode < 256)
        {
            fg = 0xFF000000u | (uint)(palette[fgCode] & 0xFFFFFF);
        }
        else
        {
            fg = defaultFg;
        }

        int bgCode = cell.Attributes.GetBgColor();
        int bgMode = cell.Attributes.GetBgColorMode();

        if (bgMode == 1) // TrueColor RGB
        {
            bg = 0xFF000000u | (uint)(bgCode & 0xFFFFFF);
        }
        else if (palette != null && bgCode >= 0 && bgCode < 256)
        {
            bg = 0xFF000000u | (uint)(palette[bgCode] & 0xFFFFFF);
        }
        else
        {
            bg = defaultBg;
        }

        if (cell.Attributes.IsInverse())
        {
            uint tmp = fg;
            fg = bg;
            bg = tmp;
        }
    }
}
