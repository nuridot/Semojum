using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Semojum.Desktop.Dialogs;

public enum LegalDocumentKind { Terms, Privacy, Marketing }

/// <summary>Offline, selectable legal documents from the bundled snapshots.</summary>
public sealed class LegalDocumentDialog : FormDialogBase
{
    public LegalDocumentKind Kind { get; }
    public FlowDocumentScrollViewer Reader { get; }
    public bool? MarketingChoice { get; private set; }

    public LegalDocumentDialog(LegalDocumentKind kind) : this(kind, LoadDocument(kind)) { }

    private LegalDocumentDialog(LegalDocumentKind kind, FlowDocument document)
        : base(TextOf(document.Blocks.FirstBlock), kind == LegalDocumentKind.Marketing ? 520 : 640, kind == LegalDocumentKind.Marketing ? 418 : 522, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        Kind = kind;
        AlignLabelBaseline();
        AutomationProperties.SetAutomationId(HeaderClose, "LegalDocumentClose");
        document.Blocks.Remove(document.Blocks.FirstBlock);
        var lead = document.Blocks.FirstBlock;
        var subtitle = new TextBlock
        {
            Text = TextOf(lead).Replace("시행일자:", "시행일자", StringComparison.Ordinal),
            Foreground = EditorModalDesign.Color("#8A8A8A"),
            FontSize = 12,
            LineHeight = 16,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap
        };
        document.Blocks.Remove(lead);
        AutomationProperties.SetAutomationId(subtitle, "LegalDocumentSubtitle");
        document.FontFamily = (FontFamily)FindResource("UiFont");
        document.FontSize = 14; document.LineHeight = 20; document.PagePadding = new(0);
        foreach (var block in document.Blocks)
        {
            if (block is Paragraph paragraph && paragraph.FontWeight == FontWeights.Bold)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
                paragraph.FontSize = 14;
            }
            block.LineHeight = 20;
            block.Margin = new Thickness(0, 0, 0, 16);
        }
        Reader = new FlowDocumentScrollViewer
        {
            Document = document,
            IsToolBarVisible = false,
            Background = Brushes.Transparent,
            BorderThickness = new(0),
            Padding = new(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = true
        };
        if (!SystemParameters.HighContrast)
        {
            var scrollStyle = new Style(typeof(ScrollBar));
            scrollStyle.Setters.Add(new Setter(WidthProperty, 8d));
            scrollStyle.Setters.Add(new Setter(OpacityProperty, 0d));
            var hover = new DataTrigger { Binding = new Binding(nameof(IsMouseOver)) { Source = Reader }, Value = true };
            hover.Setters.Add(new Setter(OpacityProperty, 1d));
            scrollStyle.Triggers.Add(hover);
            Reader.Resources[typeof(ScrollBar)] = scrollStyle;
        }
        AutomationProperties.SetAutomationId(Reader, "LegalDocumentReader");
        AutomationProperties.SetName(Reader, Title + " 본문");
        DataObject.AddCopyingHandler(Reader, OnCopy);
        Reader.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler((_, e) =>
        {
            if (e.Uri.IsAbsoluteUri && e.Uri.Scheme is "https" or "http" or "mailto")
            {
                try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                { Reader.ToolTip = "링크를 열지 못했습니다. 주소를 복사해 이용해 주세요."; }
            }
            e.Handled = true;
        }));
        var body = new DockPanel();
        DockPanel.SetDock(subtitle, Dock.Top);
        subtitle.Margin = new(0, 0, 0, 16);
        body.Children.Add(subtitle);
        if (kind == LegalDocumentKind.Marketing)
        {
            subtitle.Foreground = EditorModalDesign.Color("#5F5F5F");
            StyleMarketingSections(document, Reader);
            body.Children.Add(Reader);
            FormBody.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            FormBody.Content = body;
            FormBody.VerticalContentAlignment = VerticalAlignment.Stretch;
            var decline = FooterButton("동의 안 함", () => { MarketingChoice = false; Close(); });
            decline.Background = EditorModalDesign.Color("#FBFBFB"); decline.BorderBrush = EditorModalDesign.Color("#D1D1D1");
            FooterButton("동의", () => { MarketingChoice = true; Close(); }, true);
            KeyboardNavigation.SetTabNavigation(Root, KeyboardNavigationMode.Cycle);
            Loaded += (_, _) => Reader.Focus();
            return;
        }
        var frame = new Border
        {
            Child = Reader,
            Padding = new(16),
            CornerRadius = new(8),
            BorderThickness = new(1),
            BorderBrush = EditorModalDesign.Color("#EAEAEA"),
            Background = Brushes.White
        };
        AutomationProperties.SetAutomationId(frame, "LegalReadingSurface");
        body.Children.Add(frame);
        FormBody.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        FormBody.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        FormBody.VerticalContentAlignment = VerticalAlignment.Stretch;
        FormBody.Content = body;
        FooterButton("확인", Close, true);
        KeyboardNavigation.SetTabNavigation(Root, KeyboardNavigationMode.Cycle);
        Loaded += (_, _) => Reader.Focus();
    }

    private static void StyleMarketingSections(FlowDocument document, FlowDocumentScrollViewer reader)
    {
        var paragraphs = document.Blocks.OfType<Paragraph>().ToArray();
        document.Blocks.Clear();
        Section? section = null;
        foreach (var paragraph in paragraphs)
        {
            if (section is null || paragraph.FontWeight == FontWeights.SemiBold)
            {
                section = new Section { Padding = new(16, 11, 16, 13), Margin = new(0, 0, 0, 16),
                    Background = EditorModalDesign.Color("#F9F9F9") };
                document.Blocks.Add(section);
            }
            paragraph.Margin = new(0, section.Blocks.Count > 0 ? 4 : 0, 0, 0);
            paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            section.Blocks.Add(paragraph);
        }
        if (section is not null) section.Margin = new(0);
        // A section remains selectable across card boundaries. Paint its rounded surface at the reflowed text size.
        var backgrounds = document.Blocks.OfType<Section>().Select(s =>
        {
            var rectangle = new RectangleGeometry(new Rect(0, 0, 470, 68), 8, 8);
            s.Background = new DrawingBrush(new GeometryDrawing(EditorModalDesign.Color("#F9F9F9"), null, rectangle));
            return (Section: s, Shape: rectangle);
        }).ToArray();
        reader.LayoutUpdated += (_, _) =>
        {
            if (!reader.IsVisible || reader.ActualWidth <= 0) return;
            foreach (var (card, shape) in backgrounds)
            {
                if (!card.ContentStart.HasValidLayout || !card.ContentEnd.HasValidLayout) continue;
                var first = card.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var last = card.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
                if (first.IsEmpty || last.IsEmpty) continue;
                var size = new Rect(0, 0, reader.ActualWidth, Math.Max(1, last.Top - first.Top + 44));
                if (shape.Rect != size) shape.Rect = size;
            }
        };
    }

    private void OnCopy(object sender, DataObjectCopyingEventArgs e)
    {
        if (e.IsDragDrop || Reader.Selection.IsEmpty) return;
        var body = Reader.Document;
        var range = new TextRange(body.ContentStart, body.ContentEnd);
        if (Reader.Selection.Text != range.Text) return;
        // Copying the whole document includes the title and date now displayed above the reader.
        var original = LoadDocument(Kind);
        var whole = new TextRange(original.ContentStart, original.ContentEnd);
        var data = e.DataObject;
        data.SetData(DataFormats.UnicodeText, whole.Text);
        data.SetData(DataFormats.Text, whole.Text);
        foreach (var format in new[] { DataFormats.Rtf, DataFormats.Xaml, DataFormats.XamlPackage })
        {
            if (!data.GetDataPresent(format, false)) continue;
            var stream = new MemoryStream();
            whole.Save(stream, format);
            stream.Position = 0;
            if (format == DataFormats.XamlPackage) data.SetData(format, stream);
            else
            {
                using var reader = new StreamReader(stream, format == DataFormats.Rtf ? Encoding.ASCII : Encoding.UTF8);
                data.SetData(format, reader.ReadToEnd());
            }
        }
    }

    private static string TextOf(Block block) => new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

    private static FlowDocument LoadDocument(LegalDocumentKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var uri = new Uri($"pack://application:,,,/Semojum.Feature.Account.Wpf;component/Legal/{kind}.xaml");
        using var stream = Application.GetResourceStream(uri)?.Stream ?? throw new InvalidOperationException("약관 문서를 읽을 수 없습니다.");
        return (FlowDocument)XamlReader.Load(stream);
    }
}
