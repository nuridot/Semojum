using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;
using FormulaControl = Semojum.Desktop.Controls.FormulaPreview;
using WpfMath.Parsers;

namespace Semojum.Desktop.Dialogs;

public sealed partial class MathDialog : FormDialogBase
{
    internal TextBox Input { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    internal TextBox BrailleInput { get; } = new() { FontSize = 22, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    internal WrapPanel SymbolButtons { get; } = new();
    public string Latex => Input.Text.Trim();
    public BrailleTranslation? Translation { get; private set; }
    public bool Edited => Translation is not null && (manuallyEdited || BrailleInput.Text != Translation.Cells);
    private readonly Func<string, CancellationToken, Task<BrailleTranslation>> translate;
    private string? translatedFor;
    private int revision;
    private bool manuallyEdited, settingBraille;
    private readonly bool inlinePreview;
    public MathDialog(Func<string, CancellationToken, Task<BrailleTranslation>> translate, string? latex = null, string? manualBraille = null, bool inlinePreview = false) : base("수식 편집기", 900, 558)
    {
        this.translate = translate; this.inlinePreview = inlinePreview;
        UseControlBorder();
        Fields.Children.Add(BuildToolbox());
        var panes = new Grid { Margin = new(0, 16, 0, 0) };
        panes.ColumnDefinitions.Add(new());
        panes.ColumnDefinitions.Add(new() { Width = new(16) });
        panes.ColumnDefinitions.Add(new());
        Fields.Children.Add(panes);
        var preview = new FormulaControl { Scale = 18, FractionSlash = "⁄" };
        var formula = new Grid();
        formula.Children.Add(new Viewbox { Child = preview, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
        var previewError = new TextBlock { Text = "수식 미리보기를 표시할 수 없습니다. LaTeX 원문을 확인해 주세요.", Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        System.Windows.Automation.AutomationProperties.SetAutomationId(previewError, "MathPreviewError");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(previewError, System.Windows.Automation.AutomationLiveSetting.Polite); formula.Children.Add(previewError);
        void UpdatePreview()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Input.Text)) throw new ArgumentException("빈 수식");
                WpfTeXFormulaParser.Instance.Parse(Input.Text);
                preview.Formula = ""; preview.Formula = Input.Text;
                if (preview.HasError) throw new InvalidOperationException("Formula rendering failed.");
                preview.Visibility = Visibility.Visible; previewError.Visibility = Visibility.Collapsed;
            }
            catch
            {
                preview.Formula = ""; preview.Visibility = Visibility.Collapsed; previewError.Visibility = Visibility.Visible;
            }
        }
        panes.Children.Add(FormulaField("묵자 수식", formula, 160));
        BrailleInput.FontFamily = (FontFamily)FindResource("BraillePlainFont");
        ConfigureInput(BrailleInput, VerticalAlignment.Center);
        BrailleInput.FontSize = 22;
        var brailleField = FormulaField(inlinePreview ? "점자 수식 미리보기 (읽기 전용)" : "점자 수식", BrailleInput, 160, accent: true);
        Grid.SetColumn(brailleField, 2);
        panes.Children.Add(brailleField);
        Input.SetResourceReference(Control.FontFamilyProperty, "MonoFont");
        ConfigureInput(Input, VerticalAlignment.Top);
        var latexField = FormulaField("LaTeX", Input, 120);
        latexField.Margin = new(0, 16, 0, 0);
        Fields.Children.Add(latexField);
        System.Windows.Automation.AutomationProperties.SetName(Input, "LaTeX 수식");
        System.Windows.Automation.AutomationProperties.SetName(BrailleInput, "점자 수식");
        if (manualBraille is not null) { BrailleInput.Text = manualBraille; manuallyEdited = true; }
        BrailleInput.TextChanged += (_, _) => { if (!settingBraille) manuallyEdited = true; };
        var brailleMenu = new ContextMenu();
        var useAutomatic = new MenuItem { Header = "자동 점역 결과 사용" };
        useAutomatic.Click += (_, _) =>
        {
            if (Translation is null || translatedFor != Latex) return;
            settingBraille = true;
            try { BrailleInput.Text = Translation.Cells; manuallyEdited = false; }
            finally { settingBraille = false; }
            Status.Text = Translation.NeedsReview ? "점역 결과에 검토할 문자가 있습니다." : "";
        };
        brailleMenu.Items.Add(useAutomatic); brailleMenu.Items.Add(new Separator());
        foreach (var (label, command) in new[] { ("잘라내기", ApplicationCommands.Cut), ("복사", ApplicationCommands.Copy), ("붙여넣기", ApplicationCommands.Paste), ("모두 선택", ApplicationCommands.SelectAll) })
            brailleMenu.Items.Add(new MenuItem { Header = label, Command = command, CommandTarget = BrailleInput });
        brailleMenu.Opened += (_, _) => useAutomatic.IsEnabled = !inlinePreview && Translation is not null && translatedFor == Latex;
        BrailleInput.ContextMenu = brailleMenu;
        BrailleInput.ToolTip = inlinePreview ? "인라인 수식의 부분 점자 좌표가 없어 미리보기만 제공합니다."
            : "점자를 직접 수정할 수 있습니다. 자동 점역 결과는 우클릭 메뉴에서 다시 선택하세요.";
        Input.Text = latex ?? @"\frac{a+b}{2}=\sqrt{c}";
        if (Input.Text.StartsWith(@"\frac", StringComparison.Ordinal) && SymbolButtons.Children[0] is Button fraction)
        { fraction.Background = EditorModalDesign.Color("#EFF6FC"); fraction.BorderBrush = (Brush)FindResource("Accent"); }
        Input.TextChanged += async (_, _) =>
        {
            UpdatePreview();
            await RefreshAsync(true);
        };
        CancelButton();
        FooterButton("넣기", async () =>
        {
            if (Latex.Length == 0) throw new ArgumentException("수식을 입력해 주세요.");
            if (translatedFor != Latex) await RefreshAsync();
            if (Translation is null || translatedFor != Latex) throw new ArgumentException("점역 결과를 확인해 주세요.");
            if (!DocumentSearch.IsBrailleText(BrailleInput.Text) || string.IsNullOrWhiteSpace(BrailleInput.Text)) throw new ArgumentException("점자 수식에는 점자와 공백만 입력할 수 있습니다.");
            DialogResult = true;
        }, true);
        Loaded += async (_, _) => { UpdatePreview(); await RefreshAsync(); };
    }
    private static void ConfigureInput(TextBox box, VerticalAlignment alignment)
    {
        EditorModalDesign.Input(box);
        box.BorderThickness = new(0); box.Background = Brushes.Transparent;
        box.VerticalContentAlignment = alignment; box.Padding = new(0);
    }

    private FrameworkElement FormulaField(string title, FrameworkElement content, double height, bool accent = false)
    {
        var group = new Grid();
        var frame = new Border { Height = height, Margin = new(0, 8, 0, 0), Padding = new(16),
            CornerRadius = new(8), BorderThickness = new(accent ? 2 : 1),
            BorderBrush = EditorModalDesign.Color(accent ? "#005FB8" : "#D1D1D1"), Child = content };
        group.Children.Add(frame);
        var label = new TextBlock { Text = title, FontSize = 12, Foreground = EditorModalDesign.Color("#5F5F5F"),
            VerticalAlignment = VerticalAlignment.Center };
        group.Children.Add(new Border { Child = label, Height = 16, Padding = new(4, 0, 4, 0), Margin = new(12, 0, 0, 0),
            IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = Background });
        return group;
    }

    internal async Task RefreshAsync(bool delay = false)
    {
        var generation = ++revision; var latex = Latex; Translation = null; translatedFor = null;
        BrailleInput.IsReadOnly = true;
        try
        {
            if (delay) await Task.Delay(300, Lifetime.Token);
            if (generation != revision || latex.Length == 0) return;
            Status.Text = "점역 중…";
            var result = await translate(latex, Lifetime.Token);
            if (generation != revision || Lifetime.IsCancellationRequested) return;
            Translation = result; translatedFor = latex;
            if (!manuallyEdited)
            {
                settingBraille = true;
                try { BrailleInput.Text = result.Cells; }
                finally { settingBraille = false; }
            }
            Status.Text = inlinePreview ? "인라인 수식은 블록 전체를 다시 점역하거나 기존 점자를 유지합니다."
                : manuallyEdited ? "직접 고친 점자를 유지했습니다. 수식이 바뀌었다면 점자를 확인해 주세요."
                : result.NeedsReview ? "점역 결과에 검토할 문자가 있습니다." : "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (generation == revision) { Status.Text = ex.Message; if (!manuallyEdited) { settingBraille = true; try { BrailleInput.Clear(); } finally { settingBraille = false; } } } }
        finally { if (generation == revision) BrailleInput.IsReadOnly = inlinePreview; }
    }
}
