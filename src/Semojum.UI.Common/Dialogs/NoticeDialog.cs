using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

public enum NoticeTone { Warning, Error, Information }

/// <summary>Figma message-window geometry; action labels describe the real operation.</summary>
public class NoticeDialog : Window
{
    public new bool? ShowDialog() => Services.OwnedModal.Show(this, base.ShowDialog);
    public int Choice { get; private set; } = -1;
    public List<Button> ActionButtons { get; } = [];
    protected StackPanel MessageBody { get; } = new();
    public string MessageText { get; }
    public string DetailText { get; }
    public NoticeDialog(string message, string detail, string[] labels, bool error = false, double width = 520, double height = 180)
        : this(message, detail, labels, error ? NoticeTone.Error : NoticeTone.Warning, width, height) { }

    public NoticeDialog(string message, string detail, string[] labels, NoticeTone tone, double width = 520, double height = 180)
    {
        MessageText = message; DetailText = detail;
        var (iconName, foreground, background) = tone switch {
            NoticeTone.Information => ("info", "#005FB8", "#EFF6FC"),
            NoticeTone.Error => ("error", "#C42B1C", "#FDE7E9"),
            _ => ("warning", "#9D5D00", "#FFF4CE")
        };
        Title = "누리닷"; Width = width; Height = height; MinHeight = Height; MinWidth = width;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)FindResource(typeof(Window)); Background = FigmaDialogElements.Brush("#F3F3F3"); FontSize = 12; UseLayoutRounding = true;
        WindowChrome.SetWindowChrome(this, new() { CaptionHeight = 0, ResizeBorderThickness = new(0), GlassFrameThickness = new(0), UseAeroCaptionButtons = false });
        var root = new DockPanel(); Content = new Border { BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), BorderThickness = new(1), CornerRadius = new(8), Child = root };
        var buttons = new Grid { Margin = new(24, 1, 24, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var footer = new Border { Height = 64, Background = FigmaDialogElements.Brush("#EEEEEE"), BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), BorderThickness = new(0, 1, 0, 0), Child = buttons };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        for (var i = 0; i < labels.Length; i++)
        {
            var choice = i; var label = labels[i];
            if (i > 0) buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var destructive = label is "덮어쓰기" or "합치기" or "저장 안 함" or "삭제" or "지우기" or "요청 취소" or "탈퇴";
            var color = destructive ? "#C42B1C" : i == 0 ? "#005FB8" : "#FBFBFB";
            var filled = i == 0 || destructive;
            var button = new Button { Content = label, MinWidth = 88, Height = 32, MinHeight = 0, Padding = new(16, 0, 16, 0), Margin = new(0),
                Background = FigmaDialogElements.Brush(color), BorderBrush = FigmaDialogElements.Brush(filled ? color : "#D1D1D1"),
                BorderThickness = new(filled ? 0 : 1), Foreground = filled ? Brushes.White : FigmaDialogElements.Brush("#1A1A1A"),
                IsDefault = i == 0, IsCancel = label == "취소", Template = FigmaDialogElements.SurfaceTemplate(4, stretchContent: false, useControlInsets: true) };
            var visualIndex = labels.Length - 1 - i;
            Grid.SetColumn(button, visualIndex * 2);
            KeyboardNavigation.SetTabIndex(button, visualIndex);
            button.Click += (_, _) => { Choice = choice; DialogResult = label != "취소"; }; buttons.Children.Add(button); ActionButtons.Add(button);
        }
        var body = new ScrollViewer { Margin = new(24, 20, 24, 0), Content = MessageBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; root.Children.Add(body);
        var status = new DockPanel { LastChildFill = true, Margin = new(12, 0, 12, 0) };
        var icon = new SvgViewbox { Source = new Uri($"pack://application:,,,/Semojum.UI.Common;component/Assets/Figma/case-{iconName}.svg"), Width = 16, Height = 16, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform(1, 0) };
        var iconSlot = new Border { Width = 24, Child = icon }; DockPanel.SetDock(iconSlot, Dock.Left); status.Children.Add(iconSlot);
        status.Children.Add(new TextBlock { Text = message, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = FigmaDialogElements.Brush(foreground), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform(0, -1) });
        MessageBody.Children.Add(new Border { MinHeight = 40, CornerRadius = new(4), Background = FigmaDialogElements.Brush(background), Child = status });
        if (!string.IsNullOrWhiteSpace(detail)) MessageBody.Children.Add(new TextBlock { Text = detail, FontSize = 14, LineHeight = 20, TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 8), RenderTransform = new TranslateTransform(0, -1) });
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => Services.WindowBounds.Fit(this);
    }
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        // An input dialog can already have focused its field, for example the encrypted PDF password.
        if (IsActive && (!IsKeyboardFocusWithin || ReferenceEquals(Keyboard.FocusedElement, this)))
            ActionButtons.FirstOrDefault(button => button.IsDefault && button.IsEnabled)?.Focus();
    }
    public int ChooseFor(Window owner)
    {
        Owner = owner;
        ShowDialog();
        return Choice;
    }
    public static int Choose(Window owner, string message, string detail, string[] labels, bool error = false, double width = 520, double height = 180)
        => new NoticeDialog(message, detail, labels, error, width, height).ChooseFor(owner);
}
