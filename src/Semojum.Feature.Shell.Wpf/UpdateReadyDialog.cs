using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

/// <summary>A prepared update can be applied only after the user chooses to restart.</summary>
public sealed class UpdateReadyDialog : CompactDialog
{
    public Button RestartButton { get; }
    public Button DismissButton { get; }
    public UpdateReadyDialog(Func<Task<bool>> restart) : base("업데이트 준비 완료", 560, 66, popover: true)
    {
        UseLayoutRounding = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        var surface = (Border)Content;
        surface.CornerRadius = new(8);
        surface.BorderBrush = Brush("#EAEAEA");
        surface.Background = Brush("#FBFBFB");
        var layout = new Grid { Margin = new(16, 0, 16, 0) };
        foreach (var width in new[] { 24d, 12d, -1d, 12d, 88d, 12d, 88d })
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
        var icon = EditorModalDesign.Icon("update-ready", 24, 24, "Semojum.Feature.Shell.Wpf");
        icon.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(icon);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = "새 버전이 업데이트되었습니다.", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 4), Foreground = Brush("#1A1A1A"), TextTrimming = TextTrimming.CharacterEllipsis });
        labels.Children.Add(new TextBlock { Text = "다시 시작하면 자동으로 업데이트됩니다.", FontSize = 12, Foreground = Brush("#5F5F5F"), TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(labels, 2); layout.Children.Add(labels);
        RestartButton = new Button { Content = "다시 시작", Width = 88, Height = 32, MinHeight = 32, Padding = new(0), Background = Brush("#005FB8"), BorderBrush = Brush("#005FB8"), Foreground = Brushes.White, IsDefault = true };
        DismissButton = new Button { Content = "취소", Width = 88, Height = 32, MinHeight = 32, Padding = new(0), IsCancel = true };
        DismissButton.Background = Brush("#FBFBFB"); DismissButton.BorderBrush = Brush("#D1D1D1");
        DismissButton.Template = RestartButton.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        Grid.SetColumn(DismissButton, 4); Grid.SetColumn(RestartButton, 6); layout.Children.Add(DismissButton); layout.Children.Add(RestartButton);
        DismissButton.Click += (_, _) => Close();
        RestartButton.Click += async (_, _) =>
        {
            RestartButton.IsEnabled = DismissButton.IsEnabled = false;
            try { if (await restart() && IsVisible) Close(); }
            catch (Exception ex) { Status.Text = "업데이트를 시작하지 못했습니다: " + ex.Message; }
            finally { RestartButton.IsEnabled = DismissButton.IsEnabled = true; }
        };
        Body(layout);
        // Figma A4: the notice sits at the lower left of the screen (the editor's monitor work area), not over the editor.
        // Restarting still asks about unsaved documents before anything closes.
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) =>
        {
            if (!Services.WindowBounds.TryGetWorkArea(Owner ?? this, out var work) || PresentationSource.FromVisual(this)?.CompositionTarget is not { } target) return;
            var area = new Rect(target.TransformFromDevice.Transform(work.TopLeft), target.TransformFromDevice.Transform(work.BottomRight));
            Left = area.Left + ScreenEdge; Top = area.Bottom - Height - ScreenEdge;
        };
    }
    // Figma gives only the corner; 16 is the design-system spacing step kept between the notice and the screen edge.
    private const double ScreenEdge = 16;
    private static SolidColorBrush Brush(string value) => (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
}
