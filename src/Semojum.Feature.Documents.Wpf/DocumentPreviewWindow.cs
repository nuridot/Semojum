using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Semojum.Core;
using Semojum.Feature.Editor;
using Semojum.Desktop.Services;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

public sealed class DocumentPreviewWindow : Window
{
    private readonly SourcePreviewPage[] originals;
    private readonly string[][] faces;
    private readonly Action<DocumentPreviewWindow> printFaces;
    private readonly string documentTitle;
    private readonly Border[] sheets = new Border[2];
    private readonly RichTextBox[] readers = new RichTextBox[2];
    private readonly Button sourceMode, brailleMode;
    private readonly TextBlock modeText = new(), pageText = new();
    private readonly TextBox jump = new() { Visibility = Visibility.Collapsed, Width = 70, Height = 24, MinHeight = 0 };
    private readonly TextBlock feedback = new() { Foreground = Brushes.Firebrick, FontSize = 12 };
    private int sourceIndex, faceIndex;
    public int Face => faces.Length == 0 ? 0 : faceIndex + 1;
    public bool IsSource { get; private set; }

    public DocumentPreviewWindow(SourcePreviewPage[] originals, string[][] faces, string title, bool dirty,
        int initialSource, int initialFace, bool preferSource, string inputStatus, bool koreanGrade1, bool englishGrade1,
        Action<DocumentPreviewWindow> printFaces, string? brailleUnavailable = null)
    {
        if (originals.Length == 0 && faces.Length == 0) throw new ArgumentException("미리 볼 쪽이 없습니다.");
        this.originals = originals; this.faces = faces; this.printFaces = printFaces; documentTitle = title;
        sourceIndex = Math.Max(0, Array.FindIndex(originals, p => p.Number == initialSource));
        faceIndex = Math.Clamp(initialFace-1, 0, Math.Max(0, faces.Length-1));
        Title = (dirty ? "*" : "") + title + " · 미리 보기 - 누리닷";
        Width = 1440; Height = 1000; MinWidth = 420; MinHeight = 260; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Background = Color("#EEEEEE") };
        Content = root; WindowFrame.Apply(this, editor: true, mainEditor: true);
        var chrome = (UIElement)Content; Content = null;
        Content = new Border { BorderThickness = new(1), BorderBrush = Color("#D1D1D1"), CornerRadius = new(8), Child = chrome };
        Loaded += (_, _) => WindowBounds.Fit(this);

        var ribbon = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(8,0,0,0) };
        var toolbar = new Border { Height = 88, Background = Brushes.White, BorderThickness = new(0,0,0,1), BorderBrush = Color("#D1D1D1"), Child = ribbon };
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        sourceMode = Tool("텍스트 페이지", "preview-source", 84, () => Select(true)); sourceMode.IsEnabled = originals.Length > 0;
        brailleMode = Tool("점자 페이지", "preview-braille", 72, () => Select(false)); brailleMode.IsEnabled = faces.Length > 0;
        if (brailleUnavailable is not null) { brailleMode.ToolTip = brailleUnavailable; ToolTipService.SetShowOnDisabled(brailleMode, true); feedback.Text = brailleUnavailable; }
        ribbon.Children.Add(new Border { Width=1, Height=64, Background=Color("#EAEAEA"), Margin=new(0,0,4,0) });
        Tool("인쇄", "preview-print", 64, Print); Tool("닫기", "preview-close", 64, Close);
        Button Tool(string name, string icon, double width, Action action)
        {
            var stack = new StackPanel();
            stack.Children.Add(EditorModalDesign.Icon(icon,24,24,"Semojum.Feature.Documents.Wpf"));
            stack.Children.Add(new TextBlock { Text=name, FontSize=12, Margin=new(0,4,0,0), HorizontalAlignment=HorizontalAlignment.Center });
            var b = new Button { Width=width, Height=68, MinHeight=0, MinWidth=0, Margin=new(0,0,4,0),
                Background=Brushes.Transparent, BorderBrush=Brushes.Transparent, BorderThickness=new(1), Content=stack,
                Template=EditorModalDesign.ButtonTemplate(typeof(Button)), Padding=new(0) };
            // The toolbar icons begin eight DIPs below the button top.
            stack.Height=50; stack.VerticalAlignment=VerticalAlignment.Center;
            AutomationProperties.SetName(b,name); b.Click += (_,_) => { try { action(); } catch (Exception ex) { feedback.Text=ex.Message; } };
            ribbon.Children.Add(b); return b;
        }

        var status = new StackPanel { Orientation=Orientation.Horizontal, Margin=new(8,0,0,0) };
        var bar = new Border { Height=28, Background=Color("#F3F3F3"), BorderThickness=new(0,1,0,0), BorderBrush=Color("#EAEAEA"), Child=status };
        DockPanel.SetDock(bar,Dock.Bottom); root.Children.Add(bar);
        void Cell(FrameworkElement child)
        {
            if (status.Children.Count>0) status.Children.Add(new Border { Width=1, Height=16, Background=Color("#E1E1E1") });
            child.Margin=new(8,0,8,0); status.Children.Add(child);
        }
        TextBlock Label(string value) => new() { Text=value, FontSize=12, Foreground=Color("#5F5F5F"), VerticalAlignment=VerticalAlignment.Center };
        Cell(Label(inputStatus)); modeText.FontSize=12; modeText.VerticalAlignment=VerticalAlignment.Center; Cell(modeText);
        var pageButton = new Button { Content=pageText, Height=24, MinHeight=0, MinWidth=0, Padding=new(0), Margin=new(0),
            Background=Brushes.Transparent, BorderThickness=new(0), Foreground=Color("#5F5F5F"), Template=EditorModalDesign.ButtonTemplate(typeof(Button)),
            ToolTip="쪽 번호로 이동 · Ctrl+G" };
        AutomationProperties.SetName(pageButton,"페이지 이동"); AutomationProperties.SetName(jump,"이동할 페이지");
        pageText.FontSize=12; pageButton.Click += (_,_)=>BeginJump();
        var pageCell = new Grid(); pageCell.Children.Add(pageButton); pageCell.Children.Add(jump); Cell(pageCell);
        Cell(Label($"한글 {(koreanGrade1?1:2)}급 · 영어 {(englishGrade1?1:2)}급")); Cell(feedback);
        jump.LostKeyboardFocus += (_,_) => jump.Visibility=Visibility.Collapsed;
        jump.PreviewKeyDown += (_,e) =>
        {
            if (e.Key == Key.Escape) { jump.Visibility=Visibility.Collapsed; pageButton.Focus(); e.Handled=true; }
            else if (e.Key == Key.Enter)
            {
                e.Handled=true;
                try
                {
                    int number = DocumentNavigation.Number(jump.Text, IsSource ? originals[sourceIndex].Number : Face);
                    int i = IsSource ? Array.FindIndex(originals,p=>p.Number==number) : number-1;
                    if (i<0 || i >= Count) throw new ArgumentException("문서에 있는 페이지 번호를 입력해 주세요.");
                    SetIndex(i); jump.Visibility=Visibility.Collapsed; pageButton.Focus(); feedback.Text="";
                }
                catch (ArgumentException ex) { feedback.Text=ex.Message; jump.SelectAll(); }
            }
        };

        var spread = new StackPanel { Orientation=Orientation.Horizontal };
        for (var i=0;i<2;i++)
        {
            var reader = new RichTextBox { IsReadOnly=true, IsReadOnlyCaretVisible=false, BorderThickness=new(0), Padding=new(0),
                Background=Brushes.White, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
            readers[i]=reader;
            var sheet = new Border { Width=520, Height=700, Margin=new(i==1?24:0,0,0,0), Padding=new(40), Background=Brushes.White, Child=reader,
                Effect=new DropShadowEffect { Color=Colors.Black, Opacity=.13, ShadowDepth=2, Direction=270, BlurRadius=4 } };
            sheets[i]=sheet; spread.Children.Add(sheet);
        }
        var fit = new Viewbox { Child=spread, Stretch=Stretch.Uniform, StretchDirection=StretchDirection.DownOnly, Margin=new(8),
            HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
        var surface = new Grid { Background=Color("#EEEEEE") }; surface.Children.Add(fit); root.Children.Add(surface);
        surface.PreviewMouseWheel += (_,e) =>
        {
            if (e.Delta==0 || Keyboard.Modifiers!=ModifierKeys.None) return;
            var reader=readers.FirstOrDefault(r=>r.IsMouseOver);
            if (reader is not null && (e.Delta<0 ? reader.VerticalOffset+reader.ViewportHeight<reader.ExtentHeight-1 : reader.VerticalOffset>1)) return;
            Step(e.Delta<0?2:-2); e.Handled=true;
        };
        PreviewKeyDown += (_,e) =>
        {
            if (e.Handled || jump.IsVisible) return;
            if (e.Key==Key.G && Keyboard.Modifiers==ModifierKeys.Control) { BeginJump(); e.Handled=true; return; }
            if (Keyboard.Modifiers!=ModifierKeys.None) return;
            switch(e.Key)
            {
                case Key.PageDown: Step(2); break; case Key.PageUp: Step(-2); break;
                case Key.Right: Step(1); break; case Key.Left: Step(-1); break;
                case Key.Home: SetIndex(0); break; case Key.End: SetIndex(Count-1); break;
                case Key.Escape: Close(); break; default: return;
            }
            e.Handled=true;
        };
        Select(originals.Length>0 && (preferSource || faces.Length==0));
    }

    private static Brush Color(string code) => EditorModalDesign.Color(code);
    private int Count => IsSource ? originals.Length : faces.Length;
    private int Index => IsSource ? sourceIndex : faceIndex;
    private void Step(int delta) => SetIndex(Math.Clamp(Index+delta,0,Count-1));
    private void SetIndex(int index) { if (IsSource) sourceIndex=index; else faceIndex=index; Refresh(); }
    private void BeginJump()
    {
        jump.Visibility=Visibility.Visible; jump.Text=(IsSource?originals[sourceIndex].Number:Face).ToString(CultureInfo.CurrentCulture); jump.Focus(); jump.SelectAll();
    }
    private void Select(bool source)
    {
        IsSource=source; modeText.Text=source?"텍스트 페이지":"점자 페이지";
        void StyleMode(Button b, bool active, string icon)
        {
            b.Background=active?Color("#EFF6FC"):Brushes.Transparent; b.BorderBrush=active?Color("#005FB8"):Brushes.Transparent;
            b.Foreground=active?Color("#003E92"):Color("#1A1A1A");
            var svg=(SvgViewbox)((StackPanel)b.Content).Children[0];
            svg.Source=new Uri($"pack://application:,,,/Semojum.Feature.Documents.Wpf;component/Assets/Figma/{icon}{(active?"-on":"")}.svg");
            AutomationProperties.SetItemStatus(b,active?"선택됨":"");
        }
        StyleMode(sourceMode,source,"preview-source"); StyleMode(brailleMode,!source,"preview-braille");
        Refresh();
    }
    private void Refresh()
    {
        for (var j=0;j<sheets.Length;j++)
        {
            int i=Index+j;
            sheets[j].Visibility=i<Count?Visibility.Visible:Visibility.Hidden;
            if (i>=Count) continue;
            var reader=readers[j];
            var value=IsSource?originals[i].Text:string.Join("\n",faces[i]);
            var font=IsSource?FontFamily:(FontFamily)FindResource("BraillePlainFont");
            // RichTextBox leaves its own caret inset inside the printable area.
            reader.Margin = new Thickness(-5, IsSource ? -7 : -2, -5, 0);
            var doc=new FlowDocument { FontFamily=font, FontSize=IsSource?14:22, Foreground=Color("#1A1A1A"), PagePadding=new(0), ColumnWidth=10000,
                LineHeight=28, LineStackingStrategy=LineStackingStrategy.BlockLineHeight, TextAlignment=TextAlignment.Left };
            if (!IsSource)
            {
                var longest = faces[i].Select(l=>l.Length).DefaultIfEmpty().Max();
                var cell = new FormattedText("⠿", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 22, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
                doc.PageWidth=Math.Max(440,Math.Ceiling(cell*longest)+4);
            }
            doc.Blocks.Add(new Paragraph(new Run(value)) { Margin=new(0) });
            reader.Document=doc; reader.ScrollToHome();
            AutomationProperties.SetName(reader,IsSource?$"텍스트 {originals[i].Number}쪽":$"점자 {i+1}면");
        }
        pageText.Text=IsSource?$"{originals[sourceIndex].Number} / {Count}쪽":$"{Face} / {Count}면";
    }
    private void Print()
    {
        if (!IsSource) { printFaces(this); return; }
        var printer=new PrintDialog(); if (printer.ShowDialog()!=true) return;
        var doc=new FlowDocument { PageWidth=printer.PrintableAreaWidth, PageHeight=printer.PrintableAreaHeight,
            PagePadding=new Thickness(30), ColumnWidth=Math.Max(1,printer.PrintableAreaWidth-60), FontSize=14 };
        foreach(var page in originals) doc.Blocks.Add(new Paragraph(new Run(page.Text)) { BreakPageBefore=doc.Blocks.Count>0 });
        printer.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,documentTitle);
    }

    internal void SetFace(int page) { if (page>=1 && page<=faces.Length) { faceIndex=page-1; if (!IsSource) Refresh(); } }
}
