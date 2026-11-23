using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

internal static class RuleArticle
{
    // body_md uses paragraphs, bold labels, braille fences and GFM tables.
    internal static StackPanel Render(string markdown, FontFamily braille, string? excerpt = null)
    {
        var panel = new StackPanel();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var firstParagraph = true;
        for (var i = 0; i < lines.Length;)
        {
            var line = lines[i++];
            if (string.IsNullOrWhiteSpace(line)) continue;
            FrameworkElement block;
            if (line.TrimEnd() == "```braille")
            {
                var example = new StackPanel();
                example.Children.Add(new TextBlock { Text = "예시", FontSize = 14, FontWeight = FontWeights.SemiBold,
                    Foreground = FigmaDialogElements.Brush("#5F5F5F"), LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight });
                while (i < lines.Length && lines[i].TrimEnd() != "```")
                {
                    var lineText = lines[i++];
                    var sample = Text(lineText, braille, literal: true);
                    sample.LineHeight = lineText.Any(c => c is >= '\u2800' and <= '\u28ff') ? 28 : 20;
                    sample.Margin = new(0, 8, 0, 0);
                    example.Children.Add(sample);
                }
                if (i < lines.Length) i++;
                block = new Border { Padding = new(16, 12, 16, 12), CornerRadius = new(8), Background = FigmaDialogElements.Brush("#F9F9F9"), Child = example };
            }
            else if (line.StartsWith('|') && i < lines.Length && Regex.IsMatch(lines[i], @"^\s*\|(?:\s*:?-+:?\s*\|)+\s*$"))
            {
                var rows = new List<string[]> { Cells(line) };
                i++;
                while (i < lines.Length && lines[i].StartsWith('|')) rows.Add(Cells(lines[i++]));
                block = Table(rows, braille);
            }
            else
            {
                var paragraph = Text(line, braille);
                if (firstParagraph && excerpt != null && new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim() == excerpt.Trim()) { firstParagraph = false; continue; }
                if (firstParagraph && excerpt is null) paragraph.FontWeight = FontWeights.SemiBold;
                block = paragraph;
            }
            firstParagraph = false;
            block.Margin = new(0, panel.Children.Count == 0 ? 0 : 12, 0, 0);
            panel.Children.Add(block);
        }
        return panel;
    }

    internal static TextBlock Text(string value, FontFamily braille, bool literal = false)
    {
        var text = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        if (!literal && value.Length > 1 && value[0] == '\\' && "#>*+-_|".Contains(value[1])) value = value[1..];
        var parts = literal ? [value] : Regex.Split(value, @"(\*\*[^*\n]+\*\*)");
        foreach (var part in parts)
        {
            var bold = !literal && part.Length >= 4 && part.StartsWith("**") && part.EndsWith("**");
            foreach (var segment in Regex.Split(bold ? part[2..^2] : part, @"([\u2800-\u28ff]+)"))
            {
                if (segment.Length == 0) continue;
                var run = new Run(segment);
                if (segment[0] is >= '\u2800' and <= '\u28ff') { run.FontFamily = braille; run.FontSize = literal ? 22 : 18; }
                if (bold) run.FontWeight = FontWeights.SemiBold;
                text.Inlines.Add(run);
            }
        }
        return text;
    }

    private static string[] Cells(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var row = line.Trim();
        for (var i = 1; i < row.Length; i++)
        {
            if (row[i] == '\\' && i + 1 < row.Length && row[i + 1] is '\\' or '|') cell.Append(row[++i]);
            else if (row[i] == '|') { cells.Add(cell.ToString().Trim()); cell.Clear(); }
            else cell.Append(row[i]);
        }
        if (cell.Length > 0) cells.Add(cell.ToString().Trim());
        return cells.ToArray();
    }

    private static Border Table(List<string[]> rows, FontFamily braille)
    {
        var grid = new Grid();
        var columns = rows.Max(row => row.Length);
        var signTable = columns == 3 && rows[0].SequenceEqual(["부호", "이름", "점형"]);
        for (var col = 0; col < columns; col++)
        {
            // Cap short sign/name columns at the design widths and allow them to shrink on narrow windows.
            var column = new ColumnDefinition();
            if (signTable && col < 2) column.MaxWidth = col == 0 ? 92 : 132;
            grid.ColumnDefinitions.Add(column);
        }
        for (var i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (var col = 0; col < rows[i].Length; col++)
            {
                var text = Text(Regex.Replace(rows[i][col], @"<br\s*/?>", "\n", RegexOptions.IgnoreCase), braille);
                text.VerticalAlignment = VerticalAlignment.Center;
                if (i == 0) { text.FontWeight = FontWeights.SemiBold; text.Foreground = FigmaDialogElements.Brush("#5F5F5F"); }
                else if (rows[i][col].Any(c => c is >= '\u2800' and <= '\u28ff'))
                {
                    text.LineHeight = 28;
                    foreach (var run in text.Inlines.OfType<Run>().Where(r => r.FontFamily == braille)) run.FontSize = 22;
                }
                var cell = new Border { Child = text, MinHeight = i == 0 ? 32 : 36, Padding = new(12, 2, 0, 2),
                    Background = i == 0 ? FigmaDialogElements.Brush("#F9F9F9") : Brushes.White,
                    BorderThickness = new(0, i == 0 ? 0 : 1, 0, 0), BorderBrush = FigmaDialogElements.Brush("#EAEAEA") };
                Grid.SetRow(cell, i); Grid.SetColumn(cell, col); grid.Children.Add(cell);
            }
        }
        return new Border { CornerRadius = new(4), BorderThickness = new(1), BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), Child = grid };
    }
}
