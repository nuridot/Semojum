using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class AppliedRulesDialog : FormDialogBase
{
    internal DataGrid Rules { get; } = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single, CanUserResizeRows = false, RowHeaderWidth = 0, CanUserSortColumns = false };
    private readonly RoutedCommand openRule = new();

    public AppliedRulesDialog(SemojumApiClient api, AppliedRuleRow[] rows, bool stale, string sourceText = "", string selectedBraille = "", string? position = null)
        : base("적용 규정", 900, 418, EditorModalDesign.Icon("applied-rules-close", assemblyName:"Semojum.Feature.Rules.Wpf"))
    {
        if (position is not null) Title += " — " + position;
        UseControlBorder();
        AlignLabelBaseline();
        var preview = new Grid();
        preview.ColumnDefinitions.Add(new()); preview.ColumnDefinitions.Add(new() { Width = new(16) }); preview.ColumnDefinitions.Add(new());
        preview.Children.Add(Preview("묵자", sourceText, false));
        var braille = Preview("점자", selectedBraille.Length > 0 ? selectedBraille : string.Concat(rows.Select(r => r.Cells)), true);
        Grid.SetColumn(braille,2); preview.Children.Add(braille);
        Fields.Children.Add(preview);

        void Open(AppliedRuleRow row) => new RuleDialog(api, [], "", "", row.RuleId) { Owner = this }.ShowDialog();
        CommandBindings.Add(new CommandBinding(openRule, (_,e) =>
        {
            if (api.Account is not null && e.Parameter is AppliedRuleRow row && !string.IsNullOrWhiteSpace(row.RuleId))
            {
                Rules.SelectedItem = row;
                Open(row);
            }
        }, (_,e) => e.CanExecute = api.Account is not null && e.Parameter is AppliedRuleRow rule && !string.IsNullOrWhiteSpace(rule.RuleId)));
        Rules.Style = (Style)FindResource("AppliedRulesGrid");
        Rules.MinRowHeight = 44;
        Rules.Columns.Add(Column("걸린 자리", nameof(AppliedRuleRow.Symbol), 112));
        Rules.Columns.Add(Column("점형", nameof(AppliedRuleRow.Cells), 122, braille:true));
        Rules.Columns.Add(ReferenceColumn());
        var excerpt = Column("조항 원문", nameof(AppliedRuleRow.Text), 320);
        excerpt.Width = new(1, DataGridLengthUnitType.Star); excerpt.MinWidth = 180; Rules.Columns.Add(excerpt);
        Rules.ItemsSource = rows;
        if (rows.Length > 0) Rules.SelectedIndex = 0;
        var table = new Border { Margin = new(0,16,0,16), Height = 34 + Math.Min(3, rows.Length) * 44,
            BorderBrush = EditorModalDesign.Color("#EAEAEA"), BorderThickness = new(1), CornerRadius = new(8), Background = Brushes.White, Child = Rules };
        Rules.SizeChanged += (_,_) => Rules.Clip = new RectangleGeometry(new Rect(Rules.RenderSize),7,7);
        Fields.Children.Add(table);
        string note = rows.Length == 0
            ? stale ? "편집 후에는 적용 규정의 위치가 달라질 수 있습니다. 재점역하면 현재 결과의 근거를 확인할 수 있습니다." : "선택한 위치에 전달된 적용 규정이 없습니다."
            : "편집한 뒤에는 걸린 자리가 달라질 수 있습니다.";
        Fields.Children.Add(new TextBlock { Text = note, FontSize = 12, LineHeight = 16, Foreground = EditorModalDesign.Color("#5F5F5F"), TextWrapping = TextWrapping.Wrap });
        var reason = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = EditorModalDesign.Color("#5F5F5F"), Margin = new(0,4,0,0) };
        AutomationProperties.SetAutomationId(reason, "AppliedRulesOpenUnavailableReason");
        AutomationProperties.SetLiveSetting(reason, AutomationLiveSetting.Polite);
        Fields.Children.Add(reason);
        var openButton = LeadingFooterButton("규정 전문 열기", () =>
        {
            if (Rules.SelectedItem is AppliedRuleRow row && api.Account is not null && !string.IsNullOrWhiteSpace(row.RuleId)) Open(row);
            return Task.CompletedTask;
        },103);
        ToolTipService.SetShowOnDisabled(openButton,true);
        FooterButton("닫기", Close, true);
        void UpdateAccess()
        {
            var message = api.Account is null ? "규정 전문을 보려면 로그인해 주세요."
                : Rules.SelectedItem is not AppliedRuleRow row ? "전문을 볼 규정 행을 선택해 주세요."
                : string.IsNullOrWhiteSpace(row.RuleId) ? "선택한 규정의 식별자가 없어 전문을 열 수 없습니다." : "";
            openButton.IsEnabled = message.Length == 0;
            openButton.ToolTip = message.Length == 0 ? null : message;
            AutomationProperties.SetHelpText(openButton,message);
            reason.Text = message; reason.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        Rules.SelectionChanged += (_,_) => UpdateAccess();
        Activated += (_,_) => UpdateAccess();
        bool authenticated = api.Account is not null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_,_) =>
        {
            if (authenticated == (api.Account is not null)) return;
            authenticated = api.Account is not null;
            UpdateAccess();
            CommandManager.InvalidateRequerySuggested();
        };
        Loaded += (_,_) => timer.Start();
        Closed += (_,_) => timer.Stop();
        UpdateAccess();
    }

    private Border Preview(string label, string value, bool braille)
    {
        var group = new StackPanel();
        group.Children.Add(new TextBlock { Text = label, Height = 16, FontSize = 12, LineHeight = 16, Foreground = EditorModalDesign.Color("#5F5F5F") });
        var text = new TextBlock { Text = value, ToolTip = value, FontSize = braille ? 22 : 14, Height = braille ? 28 : 20,
            LineHeight = braille ? 28 : 20, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(0,4,0,0) };
        if (braille) text.FontFamily = (FontFamily)FindResource("BraillePlainFont");
        AutomationProperties.SetAutomationId(text,braille ? "RulesBraillePreview" : "RulesSourcePreview");
        group.Children.Add(text);
        return new Border { Child = group, Background = Brushes.White, BorderBrush = EditorModalDesign.Color("#EAEAEA"),
            BorderThickness = new(1), CornerRadius = new(8), Padding = new(16,12,16,12), VerticalAlignment = VerticalAlignment.Top };
    }

    private DataGridTextColumn Column(string title, string field, double width, bool braille = false)
    {
        var style = new Style(typeof(TextBlock), (Style)FindResource(typeof(TextBlock)));
        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, braille ? 22d : field == nameof(AppliedRuleRow.Text) ? 12d : 14d));
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(field)));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        if (braille) style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, (FontFamily)FindResource("BraillePlainFont")));
        if (field == nameof(AppliedRuleRow.Symbol))
        {
            foreach (var trigger in new[] { new DataTrigger { Binding = new Binding(field), Value = "줄 전체" },
                new DataTrigger { Binding = new Binding(nameof(AppliedRuleRow.IsBlockEvidence)), Value = true } })
            {
                trigger.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12d));
                trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, EditorModalDesign.Color("#5F5F5F")));
                style.Triggers.Add(trigger);
            }
        }
        return new DataGridTextColumn { Header = title, Binding = new Binding(field), Width = width, ElementStyle = style };
    }

    private DataGridTemplateColumn ReferenceColumn()
    {
        var link = new FrameworkElementFactory(typeof(Button));
        var caption = new Binding { Converter = new Citation() };
        link.SetBinding(ContentControl.ContentProperty,caption);
        link.SetBinding(FrameworkElement.ToolTipProperty,new Binding(nameof(AppliedRuleRow.Title)));
        link.SetValue(System.Windows.Controls.Primitives.ButtonBase.CommandProperty,openRule);
        link.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandParameterProperty,new Binding());
        link.SetValue(FrameworkElement.StyleProperty,FindResource("AppliedRuleLink"));
        return new DataGridTemplateColumn { Header = "규정", Width = 272, CellTemplate = new DataTemplate { VisualTree = link }, ClipboardContentBinding = caption };
    }

    private sealed class Citation : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not AppliedRuleRow row) return "";
            var parts = new[] { row.Source,row.Section }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            return parts.Length > 0 ? string.Join(" › ",parts) : string.IsNullOrWhiteSpace(row.Title) ? row.RuleId : row.Title;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
