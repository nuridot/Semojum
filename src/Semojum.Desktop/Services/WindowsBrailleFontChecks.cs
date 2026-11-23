using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using Semojum.Core;
using Semojum.Desktop.Controls;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

/// <summary>Braille cells render from the bundled Nuridot Braille, while other text in braille controls keeps Pretendard.</summary>
internal static class WindowsBrailleFontChecks
{
    private static readonly string SixDots = string.Concat(Enumerable.Range(0x2800, 64).Select(c => (char)c));
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var bundled = new GlyphTypeface(new Uri(BundledFonts.DirectoryUri, "NuridotBraille.ttf"));
        check(HasSixDots(bundled) && SixDots.All(c => Math.Abs(bundled.AdvanceWidths[bundled.CharacterToGlyphMap[c]] - 740d / 1024) < 1e-9),
            "The bundled Nuridot Braille font holds all 64 six-dot cells at one 740/1024 em advance");
        check(ReferenceEquals(Application.Current.FindResource("BrailleFont"), BundledFonts.Braille), "The BrailleFont resource is the bundled braille family");
        check(Faces(BundledFonts.Braille, SixDots) is [var six] && HasSixDots(six), "Every six-dot cell resolves to a Nuridot Braille glyph");
        check(Faces(BundledFonts.Braille, "⡀⣀") is [var eight] && HasSixDots(eight) && "⡀⣀".All(c => eight.CharacterToGlyphMap.ContainsKey(c)), "8-dot cells also resolve to Nuridot Braille");
        check(Faces(BundledFonts.Braille, "123<!>") is [var marks] && IsInterfaceFont(marks), "Digits and layout markers keep Pretendard instead of the braille font");

        var block = new EditorBlock { Id = 1, Source = "점자", Braille = SixDots };
        var face = new BrailleGridFace(1, [SixDots[..32].Select((c, i) => (GridCell?)new GridCell(block, i, c)).ToArray(), SixDots[32..].Select((c, i) => (GridCell?)new GridCell(block, 32 + i, c)).ToArray()]);
        var grid = new BrailleGridView { ShowFormattingMarks = true, ItemsSource = new[] { face }, Blocks = [block] };
        var strip = new LineTranslation(); strip.SetPreview(1, SixDots);
        var host = new Window { Width = 760, Height = 260, ShowInTaskbar = false, Content = new DockPanel { Children = { strip, grid } } };
        DockPanel.SetDock(strip, Dock.Bottom); host.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); host.UpdateLayout();
            var cells = Runs(grid).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
            check(DrawsBrailleFont(grid) && cells.Length == 64, "The braille grid draws all 64 six-dot cells from Nuridot Braille");
            var left = cells.Min(r => r.BaselineOrigin.X);
            check(cells.All(r =>
            {
                var ink = r.ComputeInkBoundingBox(); ink.Offset((Vector)r.BaselineOrigin); var column = (r.BaselineOrigin.X - left) / 16; var row = Math.Floor(r.BaselineOrigin.Y / 28);
                return Math.Abs(column - Math.Round(column)) < .01 && ink.Left >= r.BaselineOrigin.X && ink.Right <= r.BaselineOrigin.X + 16 && ink.Top >= row * 28 && ink.Bottom <= row * 28 + 28;
            }) && cells.Select(r => r.BaselineOrigin.X).Distinct().Count() == 32, "Each Nuridot Braille cell stays inside its 16 by 28 grid box, so cell, selection and review geometry is unchanged");
            grid.ShowFormattingMarks = false; grid.InvalidateFaces();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); host.UpdateLayout();
            check(DrawsPlainBrailleFont(grid) && Runs(grid).Where(r => r.Characters?.Any(IsSixDot) == true).All(r => r.GlyphTypeface.FamilyNames.Values.Contains("Nuridot Braille Plain")),
                "Turning off formatting marks renders the document with raised dots only");
            check(DrawsPlainBrailleFont(strip.Cells), "Line translation uses only raised dots from the embedded preview face");
        }
        finally { host.Close(); }

        var pages = new[] { new[] { new string('⠿', 32), SixDots[..32], SixDots[32..] } };
        var preview = new BraillePreviewDialog(pages, "", new PageLayout(), "글꼴 검사") { ShowInTaskbar = false }; preview.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); preview.UpdateLayout();
            var editor = Descendants(preview).OfType<TextEditor>().Single(); var view = editor.TextArea.TextView; view.EnsureVisualLines();
            var previewRuns = Runs(view).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
            check(previewRuns.Length > 0 && previewRuns.All(r => r.GlyphTypeface.FamilyNames.Values.Contains("Nuridot Braille Plain")),
                "The print preview renders only raised dots from the bundled braille face");
            var guides = editor.ContextMenu.Items.OfType<MenuItem>().Single(m => m.IsCheckable); guides.IsChecked = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); preview.UpdateLayout(); view.EnsureVisualLines();
            check(DrawsBrailleFont(view), "Inspection guides display the bundled braille face with unraised dot positions");
            var numbers = editor.TextArea.LeftMargins.OfType<LineNumberMargin>().Single();
            check(Runs(numbers).Length > 0 && Runs(numbers).All(r => IsInterfaceFont(r.GlyphTypeface)), "Braille page preview line numbers stay digits rather than Braille ASCII cells");
            var ruler = view.BackgroundRenderers.OfType<BraillePreviewDialog.CellRuler>().Single();
            var end = view.GetVisualPosition(new TextViewPosition(1, 33), ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineTop).X;
            check(Math.Abs(ruler.Offset - end) < .5 && Math.Abs(end - 32 * 22 * 740d / 1024) < .5, "Braille page preview draws the 32-cell edge after the last Nuridot Braille cell");
        }
        finally { preview.Close(); }

        var markerBlock = new EditorBlock { Id = 2, Source = "쪽", Braille = SixDots[..8] + "\n<!쪽바꿈>\n⠿" };
        var range = new RangeSelectionDialog(() => [markerBlock], SearchPanel.Braille, (_, _) => Task.CompletedTask) { ShowInTaskbar = false }; range.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); range.UpdateLayout();
            var editor = Descendants(range).OfType<TextEditor>().Single(); editor.TextArea.TextView.EnsureVisualLines();
            check(DrawsBrailleFont(editor.TextArea.TextView), "Range selection draws braille cells from Nuridot Braille");
            check(Runs(editor.TextArea.TextView).Where(r => r.Characters?.Contains('<') == true).ToArray() is { Length: > 0 } marker && marker.All(r => IsInterfaceFont(r.GlyphTypeface)),
                "Range selection keeps layout markers as readable text");
        }
        finally { range.Close(); }

        var math = new MathDialog((_, _) => Task.FromResult(BrailleTranslation.Plain("⠁")), "x", "⠿⠁") { ShowInTaskbar = false }; math.Show();
        try { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); math.UpdateLayout(); check(DrawsPlainBrailleFont(math.BrailleInput), "The formula dialog draws editable braille with the bundled raised-dot face"); }
        finally { math.Close(); }

        var symbols = new SymbolDialog([new("→", "⠒⠕", "화살표")]) { ShowInTaskbar = false }; symbols.Show();
        try { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); symbols.UpdateLayout(); check(DrawsPlainBraille(symbols), "Special symbol preview draws selected cells from the bundled raised-dot font"); }
        finally { symbols.Close(); }

        var snippets = new SnippetDialog(new(BrailleSnippets: Enumerable.Repeat("⠿⠁", 10).ToArray())) { ShowInTaskbar = false }; snippets.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); snippets.UpdateLayout();
            check(DrawsPlainBraille(snippets), "Braille snippet fields draw their cells from the bundled raised-dot font");
        }
        finally { snippets.Close(); }

        var find = new FindDialog(SearchPanel.Braille, "⠿⠯", true, (_, _) => Task.FromResult(""), (_, _, _, _) => Task.FromResult("")) { ShowInTaskbar = false }; find.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); find.UpdateLayout();
            var query = Descendants(find).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "찾을 내용");
            var runs = Runs(query).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
            check(runs.Length > 0 && runs.All(r => r.GlyphTypeface.FamilyNames.Values.Contains("Nuridot Braille Plain")
                && SixDots.All(c => r.GlyphTypeface.CharacterToGlyphMap.ContainsKey(c))), "Braille find input draws all six-dot combinations from the bundled raised-dot font");
            Descendants(find).OfType<ToggleButton>().Single(t => AutomationProperties.GetName(t) == "점자 입력").IsChecked = false;
            check(query.ReadLocalValue(Control.FontFamilyProperty) == DependencyProperty.UnsetValue, "Text find input returns to the dialog font");
        }
        finally { find.Close(); }

        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true) { ShowInTaskbar = false }; window.Show();
        try
        {
            window.ApplyAppearance(new());
            check(Faces(window.DraftBraillePreview.FontFamily, SixDots) is [var draft] && draft.FamilyNames.Values.Contains("Nuridot Braille Plain") && SixDots.All(c => draft.CharacterToGlyphMap.ContainsKey(c)),
                "The draft preview uses the complete raised-dot face");
            check(Faces(window.Braille.Editor.FontFamily, SixDots) is [var spaced] && HasSixDots(spaced) && Faces(window.Braille.Editor.FontFamily, "123") is [var digits] && IsInterfaceFont(digits),
                "Account line spacing keeps Nuridot Braille for braille cells and Pretendard for other text");
        }
        finally { window.PrepareSmokeShutdown(); window.Close(); }
    }

    private static bool DrawsPlainBraille(Visual root)
    {
        var runs = Runs(root).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
        return runs.Length > 0 && runs.All(r => r.GlyphTypeface.FamilyNames.Values.Contains("Nuridot Braille Plain"));
    }
    internal static bool DrawsPlainBrailleFont(Visual root)
    {
        var runs = Runs(root).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
        return runs.Length > 0 && runs.All(r => r.GlyphTypeface.FamilyNames.Values.Contains("Nuridot Braille Plain"));
    }
    internal static bool DrawsBrailleFont(Visual root)
    {
        var runs = Runs(root).Where(r => r.Characters?.Any(IsSixDot) == true).ToArray();
        return runs.Length > 0 && runs.All(r => HasSixDots(r.GlyphTypeface));
    }
    private static bool IsSixDot(char c) => c is >= '⠀' and <= '⠿';
    private static bool HasSixDots(GlyphTypeface face) => Path.GetFileName(face.FontUri.LocalPath).Equals("NuridotBraille.ttf", StringComparison.OrdinalIgnoreCase)
        && face.FamilyNames.Values.Contains("Nuridot Braille") && SixDots.All(c => face.CharacterToGlyphMap.ContainsKey(c));
    private static bool IsInterfaceFont(GlyphTypeface face) => Path.GetFileName(face.FontUri.LocalPath).Equals("Pretendard-Regular.ttf", StringComparison.OrdinalIgnoreCase);
    private static GlyphTypeface[] Faces(FontFamily family, string text)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 22, Brushes.Black, 1), new Point());
        return Runs(visual).Select(r => r.GlyphTypeface).Distinct().ToArray();
    }
    private static GlyphRun[] Runs(Visual root) => Descendants(root).OfType<Visual>().SelectMany(v => Drawings(VisualTreeHelper.GetDrawing(v))).OfType<GlyphRunDrawing>().Select(d => d.GlyphRun).ToArray();
    private static IEnumerable<Drawing> Drawings(Drawing? drawing)
    {
        if (drawing is null) yield break;
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Drawings(child)) yield return item;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
}
