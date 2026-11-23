using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpVectors.Converters;

namespace Semojum.Desktop.Controls;

/// <summary>Empty original panel file picker and drop target.</summary>
internal sealed class OriginalUploadView : UserControl
{
    internal Button UploadArea { get; } = new();
    private readonly Border limitWarning = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock limitTitle = new();
    internal event Action<string[]?>? UploadRequested;
    internal OriginalUploadView()
    {
        Background = Brushes.White;
        var root = new Grid { Margin = new Thickness(17, 21, 17, 21) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        var region = UploadArea;
        region.Height = double.NaN; // Override the shared 32px button style.
        region.VerticalAlignment = VerticalAlignment.Stretch;
        region.Padding = new Thickness(0);
        region.BorderThickness = new Thickness(0);
        region.Background = Brushes.Transparent;
        region.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        region.VerticalContentAlignment = VerticalAlignment.Stretch;
        region.AllowDrop = true;
        const string supportedFiles = "PDF · HWP · HWPX · TXT · JPG · JPEG · PNG · SMJ";
        region.ToolTip = $"파일을 선택하거나 이 영역에 놓으세요. 지원 형식: {supportedFiles}";
        System.Windows.Automation.AutomationProperties.SetHelpText(region, supportedFiles);
        System.Windows.Automation.AutomationProperties.SetName(region, "원본 파일 선택 또는 놓기");
        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
        region.Template = template;
        var surface = new Grid();
        surface.Children.Add(new Border { Background = ColorBrush(0xEF, 0xF6, 0xFC), CornerRadius = new CornerRadius(8) });
        surface.Children.Add(new Rectangle { Stroke = ColorBrush(0x00, 0x5F, 0xB8), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 6, 4 }, RadiusX = 8, RadiusY = 8, IsHitTestVisible = false });
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 20, 0), IsHitTestVisible = false };
        content.Children.Add(Icon("original-upload.svg", 24));
        content.Children.Add(new TextBlock { Text = "PDF 파일을 드래그하거나 클릭해서 선택하세요", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = ColorBrush(0x1A, 0x1A, 0x1A), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), LineHeight = 20 });
        content.Children.Add(new TextBlock { Text = "파일 1개당 최대 100MB", FontSize = 12, Foreground = ColorBrush(0x5F, 0x5F, 0x5F), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 12, 0, 0) });
        content.Children.Add(new TextBlock { Text = "PDF · HWP · HWPX · TXT · JPG · PNG · SMJ", FontSize = 12, Foreground = ColorBrush(0x5F, 0x5F, 0x5F), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), LineHeight = 16 });
        content.Children.Add(new Border { Background = ColorBrush(0x00, 0x5F, 0xB8), CornerRadius = new CornerRadius(4), Width = 88, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0), Child = new TextBlock { Text = "파일 선택", FontSize = 14, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        surface.Children.Add(content); region.Content = surface;
        root.Children.Add(region);
        limitWarning.Margin = new Thickness(0, 12, 0, 0);
        limitWarning.Padding = new Thickness(12, 8, 12, 8);
        limitWarning.Background = ColorBrush(0xFD, 0xE7, 0xE9); limitWarning.BorderBrush = ColorBrush(0xC4, 0x2B, 0x1C);
        limitWarning.BorderThickness = new Thickness(1); limitWarning.CornerRadius = new CornerRadius(4);
        var warningGrid = new DockPanel();
        var warningIcon = Icon("original-upload-warning.svg", 16);
        warningIcon.Margin = new Thickness(0, 0, 8, 0);
        warningGrid.Children.Add(warningIcon);
        limitTitle.FontSize = 12;
        limitTitle.Foreground = ColorBrush(0xC4, 0x2B, 0x1C);
        limitTitle.TextWrapping = TextWrapping.Wrap;
        limitTitle.VerticalAlignment = VerticalAlignment.Center;
        warningGrid.Children.Add(limitTitle);
        limitWarning.Child = warningGrid;
        System.Windows.Automation.AutomationProperties.SetLiveSetting(limitWarning, System.Windows.Automation.AutomationLiveSetting.Assertive);
        Grid.SetRow(limitWarning, 1); root.Children.Add(limitWarning);

        DataContextChanged += (_, _) => ClearLimitWarning();
        region.Click += (_, _) => UploadRequested?.Invoke(null);
        region.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        region.Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) UploadRequested?.Invoke(paths); e.Handled = true; };

    }
    /// <summary>Names the first oversized file and its size (Figma C3); other oversized files are counted and listed in the tooltip.</summary>
    internal void ShowLimitWarning(IReadOnlyList<(string Path, long Bytes)> oversized)
    {
        limitTitle.Text = Label(oversized[0]) + (oversized.Count > 1 ? $" 외 {oversized.Count - 1}개" : "") + " · 100MB 를 넘습니다";
        limitTitle.ToolTip = string.Join("\n", oversized.Select(Label));
        limitWarning.Visibility = Visibility.Visible;
    }
    /// <summary>Several images over the shared 100 MB total (BE 2026-11-02): names the count and total; each file is in the tooltip.</summary>
    internal void ShowTotalLimitWarning(IReadOnlyList<(string Path, long Bytes)> images)
    {
        limitTitle.Text = $"이미지 {images.Count}장 합계 ({Megabytes(images.Sum(file => file.Bytes))}MB) · 100MB 를 넘습니다";
        limitTitle.ToolTip = string.Join("\n", images.Select(Label));
        limitWarning.Visibility = Visibility.Visible;
    }
    private static string Label((string Path, long Bytes) file) => $"{System.IO.Path.GetFileName(file.Path)} ({Megabytes(file.Bytes)}MB)";
    private static string Megabytes(long bytes) => $"{Math.Ceiling(bytes / (1024d * 1024d)):0}";
    internal void ClearLimitWarning() => limitWarning.Visibility = Visibility.Collapsed;
    private static SolidColorBrush ColorBrush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private static SvgViewbox Icon(string file, double size) => new() { Source = new Uri($"pack://application:,,,/Semojum.Feature.Editor.Wpf;component/Assets/Figma/{file}"), Width = size, Height = size, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center };
}
