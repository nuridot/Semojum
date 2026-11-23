using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Semojum.Core;

namespace Semojum.Desktop.Dialogs;

public sealed class ExportRangeDialog : FormDialogBase
{
    private readonly int[] originalNumbers;
    private readonly Func<Task<int>>? brailleCount;
    private readonly bool allowFormatChange;
    private readonly string initialFormat;
    private readonly RadioButton source = Choice("원본 페이지"), braille = Choice("점자 페이지");
    private readonly StackPanel formats = new() { Orientation = Orientation.Horizontal };
    private readonly TextBox start = new(), end = new();
    private readonly FrameworkElement rangeFields;
    private readonly TextBlock bounds = new() { FontSize = 12, Margin = new(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button save;
    private Task<int>? pendingCount;
    private int? pageCount;
    private int boundsRevision;
    public ExportRange Result { get; private set; } = new();
    public string SelectedFormat { get; private set; }

    public ExportRangeDialog(int[] originalNumbers, Func<Task<int>>? brailleCount, bool brailleOnly = false, string format = ".brf", bool allowFormatChange = false)
        : base("범위 지정 저장", 560, 250, EditorModalDesign.Icon("range-close", assemblyName: "Semojum.Feature.Documents.Wpf"))
    {
        this.originalNumbers = originalNumbers; this.brailleCount = brailleCount;
        this.allowFormatChange = allowFormatChange; initialFormat = SelectedFormat = format;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
        AlignLabelBaseline();

        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        var scope = new StackPanel { Orientation = Orientation.Horizontal };
        AutomationProperties.SetName(scope, "범위 기준");
        scope.Children.Add(source); scope.Children.Add(braille);
        FormRow(rows, "기준", scope, 162);
        var numbers = new WrapPanel();
        numbers.Children.Add(NumberField(start, "시작 페이지 번호", 1, int.MaxValue));
        var separator = new TextBlock
        {
            Text = "~",
            Width = 33,
            TextAlignment = TextAlignment.Center,
            FontSize = 14,
            Foreground = EditorModalDesign.Color("#5F5F5F"),
            VerticalAlignment = VerticalAlignment.Center
        };
        numbers.Children.Add(separator);
        numbers.Children.Add(NumberField(end, "끝 페이지 번호", 1, int.MaxValue));
        bounds.Foreground = EditorModalDesign.Color("#5F5F5F");
        numbers.Children.Add(bounds);
        rangeFields = numbers;
        FormRow(rows, "시작 · 끝", numbers, 162);
        AutomationProperties.SetName(formats, "형식");
        FormRow(rows, "형식", formats, 162);
        CancelButton();
        var cancel = Actions.Children.OfType<Button>().Single();
        cancel.Background = EditorModalDesign.Color("#FBFBFB"); cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        save = FooterButton("저장", () =>
        {
            if (braille.IsChecked == true && pageCount is null)
            {
                _ = UpdateBoundsAsync();
                return;
            }
            if (Apply()) DialogResult = true;
        }, true);

        var sourceFormat = format is ".txt — 원문" or ".md";
        var fixedBraille = !allowFormatChange && !sourceFormat && brailleCount is not null;
        source.IsEnabled = !brailleOnly && !fixedBraille && originalNumbers.Length > 0;
        braille.IsEnabled = brailleCount is not null && (allowFormatChange || brailleOnly || fixedBraille);
        braille.ToolTip = "표지를 포함한 첫 면부터 셉니다. 인쇄된 쪽 번호와 다를 수 있습니다.";
        source.Checked += ScopeChanged; braille.Checked += ScopeChanged;
        if (source.IsEnabled || !braille.IsEnabled) source.IsChecked = true;
        else braille.IsChecked = true;
    }

    private static RadioButton Choice(string label)
    {
        var radio = new RadioButton { Content = new TextBlock { Text = label }, Height = 20, Margin = new(0, 0, 16, 0) };
        AutomationProperties.SetName(radio, label);
        return radio;
    }

    private async void ScopeChanged(object sender, RoutedEventArgs e)
    {
        var options = source.IsChecked == true ? new[] { ".txt — 원문", ".md" } : new[] { ".brf", ".txt — 점자" };
        var current = options.Contains(initialFormat) ? initialFormat : options[0];
        SelectedFormat = current;
        formats.Children.Clear();
        foreach (var value in options)
        {
            var option = Choice(value.Replace(" — ", " "));
            option.Tag = value;
            option.IsEnabled = allowFormatChange || value == current;
            option.IsChecked = value == current;
            option.Checked += (_, _) => SelectedFormat = value;
            formats.Children.Add(option);
        }
        await UpdateBoundsAsync();
    }

    private async Task UpdateBoundsAsync()
    {
        var revision = ++boundsRevision;
        Status.Text = ""; save.ToolTip = null;
        if (source.IsChecked == true)
        {
            bounds.Visibility = Visibility.Collapsed;
            rangeFields.IsEnabled = save.IsEnabled = originalNumbers.Length > 0;
            start.Text = originalNumbers.FirstOrDefault().ToString(); end.Text = originalNumbers.LastOrDefault().ToString();
            if (originalNumbers.Length == 0) Status.Text = "저장할 원본 페이지가 없습니다.";
            return;
        }
        bounds.Visibility = Visibility.Visible;
        if (pageCount is null && brailleCount is not null)
        {
            rangeFields.IsEnabled = save.IsEnabled = false;
            bounds.Text = "계산 중…"; start.Clear(); end.Clear();
            try
            {
                pendingCount ??= brailleCount();
                pageCount = await pendingCount;
            }
            catch (Exception ex)
            {
                if (pendingCount is { IsFaulted: true } or { IsCanceled: true }) pendingCount = null;
                if (revision != boundsRevision || Lifetime.IsCancellationRequested) return;
                if (braille.IsChecked == true)
                {
                    Status.Text = ex.Message; bounds.Text = "계산 실패";
                    save.IsEnabled = true; save.ToolTip = "다시 눌러 면 수를 계산합니다.";
                }
                return;
            }
        }
        if (revision != boundsRevision || Lifetime.IsCancellationRequested || braille.IsChecked != true) return;
        var count = pageCount.GetValueOrDefault();
        rangeFields.IsEnabled = save.IsEnabled = count > 0; save.ToolTip = null;
        start.Text = "1"; end.Text = count.ToString(); bounds.Text = $"총 {count}면";
        if (count < 1) Status.Text = "저장할 점자 면이 없습니다.";
    }

    private bool Apply()
    {
        var original = source.IsChecked == true;
        if (!int.TryParse(start.Text, out var from) || !int.TryParse(end.Text, out var to))
        { Status.Text = "시작과 끝 번호를 정수로 입력해 주세요."; return false; }
        try
        {
            var range = new ExportRange(original ? ExportScope.OriginalPages : ExportScope.BraillePages, from, to);
            range.Validate(original ? originalNumbers.FirstOrDefault() : 1, original ? originalNumbers.LastOrDefault() : pageCount.GetValueOrDefault());
            Result = range;
            return true;
        }
        catch (ArgumentException ex)
        {
            var notice = FileNotices.InvalidRange(from, to, ex.Message); notice.Owner = this; notice.ShowDialog();
            if (notice.Choice != 0) Close(); else { start.Focus(); start.SelectAll(); }
            return false;
        }
    }
}
