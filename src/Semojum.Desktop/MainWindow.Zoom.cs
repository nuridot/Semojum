using System.Windows;
using System.Windows.Input;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private enum ZoomPanel { Source, Braille, Original }
    private ZoomPanel zoomPanel = ZoomPanel.Source;
    private bool updatingZoom;
    private (double Min, double Normal, double Max) ZoomScale => zoomPanel switch
    {
        ZoomPanel.Original => (25, 100, 400),
        ZoomPanel.Braille => (8, appearance.BrailleSize, 72),
        _ => (Math.Min(10, appearance.TextSize), appearance.TextSize, Math.Max(32, appearance.TextSize))
    };
    private double PanelSize => zoomPanel switch
    {
        ZoomPanel.Original => originalZoom * 100,
        ZoomPanel.Braille => Braille.Editor.FontSize,
        _ => SourceEditor.FontSize
    };

    private void ActivateZoom(ZoomPanel panel)
    {
        if (zoomPanel == panel) return;
        zoomPanel = panel;
        PaintPanelHeaders();
        RefreshZoomControls();
        RefreshEditorStatus();
    }

    private void ConnectPanelZoom(Semojum.Feature.Editor.Wpf.PanelPageBar bar, System.Windows.Controls.Border header, ZoomPanel panel)
    {
        header.SizeChanged += (_, e) => bar.SetCompact(e.NewSize.Width < 370);
        bar.ConfigureZoom(panel == ZoomPanel.Source ? "묵자" : "점자", direction =>
        {
            ActivateZoom(panel);
            StepPanelZoom(direction);
        }, anchor =>
        {
            ActivateZoom(panel);
            PanelZoomPopup.PlacementTarget = anchor;
            PanelZoomPopup.IsOpen = true;
            ZoomSlider.Focus();
        });
    }

    private void SetBrailleSize(double size)
    {
        Braille.Editor.FontSize = Braille.GridView.FontSize = size;
        Braille.GridView.InvalidateFaces();
        if (detachedPane is null) return;
        detachedPane.Editor.FontSize = detachedPane.GridView.FontSize = size;
        detachedPane.GridView.InvalidateFaces();
    }

    private void SetPanelSize(double size)
    {
        if (updatingZoom || SourceEditor is null) return;
        var scale = ZoomScale;
        size = Math.Clamp(size, scale.Min, scale.Max);
        switch (zoomPanel)
        {
            case ZoomPanel.Original: SetOriginalZoom(size / 100); return;
            case ZoomPanel.Braille: SetBrailleSize(size); break;
            default: SourceEditor.FontSize = PageEditor.Editor.FontSize = size; break;
        }
        RefreshZoomControls();
    }

    private void StepPanelZoom(int direction) => SetPanelSize(zoomPanel == ZoomPanel.Original
        ? PanelSize * (direction > 0 ? 1.25 : .8) : PanelSize + direction * 2);

    private void PanelZoom_KeyDown(object sender, KeyEventArgs e)
    {
        if (TryPanelZoomKey(e.Key, Keyboard.Modifiers)) e.Handled = true;
    }
    internal bool TryPanelZoomKey(Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.Control && modifiers != (ModifierKeys.Control | ModifierKeys.Shift)) return false;
        var direction = key switch { Key.OemPlus or Key.Add => 1, Key.OemMinus or Key.Subtract => -1, _ => 0 };
        if (direction == 0) return false;
        StepPanelZoom(direction);
        return true;
    }

    private void PanelZoom_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled && TryPanelZoomWheel(e.Delta, Keyboard.Modifiers)) e.Handled = true;
    }
    internal bool TryPanelZoomWheel(int delta, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.Control || delta == 0) return false;
        StepPanelZoom(Math.Sign(delta));
        return true;
    }

    private void RefreshZoomControls()
    {
        if (updatingZoom || ZoomSlider is null || SourceEditor is null || FontSizeLabel is null) return;
        updatingZoom = true;
        try
        {
            var size = PanelSize;
            var scale = ZoomScale;
            var original = zoomPanel == ZoomPanel.Original;
            string Label(double n) => n.ToString("0.#") + (original ? "%" : "");
            double[] choices = original ? [25, 50, 75, 100, 125, 150, 200, 300, 400] : [12, 14, 16, 18, 20, 22, 24, 30, 32];
            FontSizeLabel.ItemsSource = choices.Append(size).Distinct().Order().Select(Label).Distinct().ToArray();
            FontSizeLabel.SelectedItem = Label(size);
            FontSizeLabel.ToolTip = original ? "원본 배율" : zoomPanel == ZoomPanel.Source ? "텍스트 글자 크기" : "점자 글자 크기";
            var percent = (size / scale.Normal * 100).ToString("0") + "%";
            SourcePageBar.SetZoomPercent((SourceEditor.FontSize / appearance.TextSize * 100).ToString("0") + "%");
            var braillePercent = (Braille.Editor.FontSize / appearance.BrailleSize * 100).ToString("0") + "%";
            Braille.FacePageBar.SetZoomPercent(braillePercent);
            detachedPane?.FacePageBar.SetZoomPercent(braillePercent);
            ZoomPicker.Text = ZoomPercent.Text = percent;
            ZoomSlider.Value = size == scale.Normal ? 50 : size < scale.Normal
                ? (size - scale.Min) * 50 / (scale.Normal - scale.Min)
                : 50 + (size - scale.Normal) * 50 / (scale.Max - scale.Normal);
        }
        finally { updatingZoom = false; }
    }

    private void ZoomPicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!updatingZoom && ZoomPicker.SelectedItem is string text && double.TryParse(text.TrimEnd('%'), out var percent))
            SetPanelSize(ZoomScale.Normal * percent / 100);
    }
    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updatingZoom || SourceEditor is null) return;
        var (min, normal, max) = ZoomScale;
        SetPanelSize(e.NewValue <= 50 ? min + e.NewValue * (normal - min) / 50 : normal + (e.NewValue - 50) * (max - normal) / 50);
    }
    private void FontSizePicker_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!updatingZoom && FontSizeLabel.SelectedItem is string text && double.TryParse(text.TrimEnd('%'), out var size))
            SetPanelSize(size);
    }
}
