using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Win32;
using Semojum.Core;
using Semojum.Desktop.Services;
using Semojum.Feature.Editor;

namespace Semojum.Desktop.Dialogs;

public sealed record SourcePreviewPage(int Number, string Text);

public sealed class BraillePreviewDialog : ConversionFormDialog
{
    private readonly TextEditor editor = new() { IsReadOnly = true, FontFamily = (FontFamily)Application.Current.FindResource("BraillePlainFont"),
        FontSize = 22, WordWrap = false, Padding = new(0), BorderThickness = new(0), Background = Brushes.White,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    public int Page { get; private set; }

    public BraillePreviewDialog(string[][] pages, string brf, PageLayout layout, string title, int initial = 1,
        int[]? spacingUnknownBlockIds = null, Func<NoticeDialog, int>? confirmSpacing = null)
        : this(pages, brf, layout, title, initial, spacingUnknownBlockIds, null, confirmSpacing) { }

    public BraillePreviewDialog(string[][] pages, string brf, PageLayout layout, string title, int initial,
        int[]? spacingUnknownBlockIds, int[]? alignmentUnknownBlockIds, Func<NoticeDialog, int>? confirmSpacing = null)
        : base("인쇄", 720, 632)
    {
        if (pages.Length == 0) throw new ArgumentException("미리 볼 점자 면이 없습니다.");
        var number = new TextBox();
        var caption = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center };
        var unknown = spacingUnknownBlockIds ?? [];
        var unknownAlign = alignmentUnknownBlockIds ?? [];
        var blockList = string.Join(", ", unknown.Take(10)) + (unknown.Length > 10 ? $" 외 {unknown.Length - 10}개" : "");
        var alignmentList = string.Join(", ", unknownAlign.Take(10)) + (unknownAlign.Length > 10 ? $" 외 {unknownAlign.Length - 10}개" : "");
        var warnings = new List<string>();
        if (unknown.Length > 0) warnings.Add("직접 고친 점자의 빈 줄이 조판 빈 줄과 겹치거나 엔진이 쪽 빈 줄을 정하지 못했습니다. 빈 줄을 확인해 주세요. 블록: " + blockList);
        if (unknownAlign.Length > 0) warnings.Add("정렬 태그와 점자 줄의 대응을 확인할 수 없어 해당 정렬을 적용하지 않았습니다. 블록: " + alignmentList);
        var spacingMessage = string.Join("\n", warnings);
        bool ConfirmSpacing()
        {
            if (warnings.Count == 0) return true;
            var notice = new NoticeDialog(unknownAlign.Length > 0 ? "조판 정보를 확인할 수 없는 블록이 있습니다." : "조판 빈 줄을 확인해 주세요.",
                spacingMessage + "\n현재 판면을 출력하시겠습니까?", ["출력", "취소"], height: warnings.Count > 1 ? 266 : 226);
            if (confirmSpacing is { } decide) return decide(notice) == 0;
            notice.Owner = this; notice.ShowDialog(); return notice.Choice == 0;
        }
        void Show(int page)
        {
            if (page < 1 || page > pages.Length) throw new ArgumentException($"1–{pages.Length}면 안에서 입력해 주세요.");
            Page = page; number.Text = page.ToString(); editor.Text = string.Join("\n", pages[page - 1]); editor.ScrollToHome();
            caption.Text = $"{page} / {pages.Length}면 · 번호 {layout.StartPage + page - 1} · {layout.Columns}칸 × {layout.Lines}줄";
            caption.ToolTip = caption.Text; Status.Text = "";
        }
        var navigation = new DockPanel { Height = 32 }; Fields.Children.Add(navigation);
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(controls, Dock.Right); navigation.Children.Add(controls); navigation.Children.Add(caption);
        void NavButton(string name, Action action)
        {
            var b = Button(name, () => RunAsync(() => { action(); return Task.CompletedTask; }));
            b.Width = b.MinWidth = 88; b.Template = EditorModalDesign.ButtonTemplate(typeof(Button)); controls.Children.Add(b);
        }
        NavButton("이전", () => Show(Math.Max(1, Page-1)));
        var field = NumberField(number, "이동할 점자 면", 1, pages.Length); field.Margin = new(8,0,0,0); controls.Children.Add(field);
        void Go() => Show(DocumentNavigation.Number(number.Text, Page));
        NavButton("이동", Go); NavButton("다음", () => Show(Math.Min(pages.Length, Page+1)));
        number.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            e.Handled = true; await RunAsync(() => { Go(); return Task.CompletedTask; });
        };
        if (warnings.Count > 0) Fields.Children.Add(new TextBlock { Text = spacingMessage, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Firebrick, Margin = new(0,8,0,0) });
        var guides = new MenuItem { Header = "줄 번호와 칸 경계 표시", IsCheckable = true };
        var ruler = new CellRuler(editor, layout.Columns);
        guides.Checked += (_, _) => { editor.FontFamily = (FontFamily)FindResource("BrailleFont"); editor.ShowLineNumbers = true; editor.TextArea.TextView.BackgroundRenderers.Add(ruler); editor.TextArea.TextView.Redraw(); };
        guides.Unchecked += (_, _) => { editor.FontFamily = (FontFamily)FindResource("BraillePlainFont"); editor.ShowLineNumbers = false; editor.TextArea.TextView.BackgroundRenderers.Remove(ruler); editor.TextArea.TextView.Redraw(); };
        editor.ContextMenu = new ContextMenu(); editor.ContextMenu.Items.Add(guides);
        editor.ContextMenu.Items.Add(new MenuItem { Header = "복사", Command = System.Windows.Input.ApplicationCommands.Copy, CommandTarget = editor.TextArea });
        System.Windows.Automation.AutomationProperties.SetName(editor, "점자 면");
        var paper = new Border { Margin = new(0,16,0,0), Height = 454, Padding = new(24,16,24,16), Background = Brushes.White,
            BorderThickness = new(1), BorderBrush = EditorModalDesign.Color("#EAEAEA"), CornerRadius = new(4), Child = editor };
        Fields.Children.Add(paper);
        async Task Save(bool txt)
        {
            if (!ConfirmSpacing()) return;
            var range = new ExportRangeDialog([], () => Task.FromResult(pages.Length), brailleOnly: true, format: txt ? ".txt — 점자" : ".brf") { Owner = this };
            if (range.ShowDialog() != true) return;
            var dialog = new SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(title), Filter = txt ? "점자 텍스트|*.txt" : "BRF|*.brf" };
            if (dialog.ShowDialog(this) != true) return;
            var output = ExportContent(brf, pages, range.Result, txt);
            var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temp, output, new UTF8Encoding(false)); File.Move(temp, dialog.FileName, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        FooterButton("TXT 저장", () => Save(true)); FooterButton("BRF 저장", () => Save(false));
        FooterButton("인쇄", async () => { if (ConfirmSpacing()) await RawBraillePrinter.PrintAsync(this, brf, title); }, true);
        FooterButton("닫기", Close);
        Show(initial);
    }
    internal static string ExportContent(string brf, string[][] pages, ExportRange range, bool txt)
    {
        if (!txt) return DocumentExport.SelectBraille(brf, range);
        range.Validate(1, pages.Length);
        return DocumentExport.BrailleText(range.Scope == ExportScope.All ? pages : pages.Skip(range.Start - 1).Take(range.End - range.Start + 1));
    }

    /// <summary>AvalonEdit's own ruler sits at columns × the width of "x"; the page edge belongs after the last braille cell.</summary>
    internal sealed class CellRuler(TextEditor editor, int columns) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;
        internal double Offset => new FormattedText(new string('⠿', columns), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch), editor.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(editor).PixelsPerDip).WidthIncludingTrailingWhitespace;
        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var x = Math.Round(Offset - textView.ScrollOffset.X) + .5;
            drawingContext.DrawLine(new Pen(Brushes.Gray, 1), new(x, 0), new(x, Math.Max(textView.DocumentHeight - textView.VerticalOffset, textView.ActualHeight)));
        }
    }
}
