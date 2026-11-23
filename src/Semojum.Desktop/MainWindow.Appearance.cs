using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private void SetActiveEditor(TextEditor? editor)
    {
        ClearOriginalRegion();
        lastGrid = null;
        if (editor is not null) lastEditor = editor;
        var panel = editor is null ? ZoomPanel.Original : editor == SourceEditor || editor == PageEditor.Editor ? ZoomPanel.Source : ZoomPanel.Braille;
        ActivateZoom(panel);
        RefreshLinkedSource();
        RefreshEditorStatus();
    }

    private void PaintPanelHeaders()
    {
        Paint(OriginalHeader, OriginalPanelTitle, OriginalNumberBadge, OriginalPanelNumber, zoomPanel == ZoomPanel.Original);
        Paint(SourceHeader, SourcePanelTitle, SourceNumberBadge, SourcePanelNumber, zoomPanel == ZoomPanel.Source);
        Paint(Braille.Header, Braille.PanelTitle, Braille.NumberBadge, Braille.PanelNumber, zoomPanel == ZoomPanel.Braille);
        if (detachedPane is { } pane) Paint(pane.Header, pane.PanelTitle, pane.NumberBadge, pane.PanelNumber, zoomPanel == ZoomPanel.Braille);

        static void Paint(Border header, TextBlock title, Border badge, TextBlock number, bool active)
        {
            header.Background = active ? Brushes.White : NeutralHeader;
            header.BorderBrush = active ? HeaderAccent : HeaderLine;
            header.BorderThickness = active ? new Thickness(0, 2, 0, 0) : new Thickness(0, 0, 0, 1);
            title.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            title.Foreground = active ? HeaderInk : HeaderMuted;
            badge.Background = active ? HeaderAccent : NeutralBadge;
            number.Foreground = active ? Brushes.White : HeaderMuted;
        }
    }

    private static readonly Brush HeaderLine = new SolidColorBrush(Color.FromRgb(234, 234, 234));
    private static readonly Brush NeutralHeader = new SolidColorBrush(Color.FromRgb(249, 249, 249));
    private static readonly Brush HeaderInk = new SolidColorBrush(Color.FromRgb(26, 26, 26));
    private static readonly Brush HeaderMuted = new SolidColorBrush(Color.FromRgb(95, 95, 95));
    private static readonly Brush HeaderAccent = new SolidColorBrush(Color.FromRgb(0, 95, 184));
    private static readonly Brush NeutralBadge = new SolidColorBrush(Color.FromRgb(240, 240, 240));

    // Figma 263:7469 「입력 한글」 is the keyboard's 한/영 state; an element with its IME off takes English input.
    private void InitializeInputStatus()
    {
        // The 한/영 key, another input language or another focused control can each change it.
        InputMethodStateChangedEventHandler imeChanged = (_, _) => RefreshInputStatus();
        InputLanguageEventHandler languageChanged = (_, _) => RefreshInputStatus();
        InputMethod.Current.StateChanged += imeChanged; InputLanguageManager.Current.InputLanguageChanged += languageChanged;
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, _) => RefreshInputStatus()), true);
        // An IMM32 IME changes its mode without TSF notifications.
        PreviewKeyUp += (_, e) => { if (e.Key is Key.ImeProcessed or Key.HangulMode || e.ImeProcessedKey == Key.HangulMode) RefreshInputStatus(); };
        Closed += (_, _) => { InputMethod.Current.StateChanged -= imeChanged; InputLanguageManager.Current.InputLanguageChanged -= languageChanged; };
    }
    private void RefreshInputStatus()
    {
        if (StatusInput is null) return;
        var ime = Keyboard.FocusedElement is not DependencyObject focused || InputMethod.GetIsInputMethodEnabled(focused);
        StatusInput.Text = InputStatusText(InputLanguageManager.Current.CurrentInputLanguage, ime ? InputMethod.Current.ImeConversionMode : ImeConversionModeValues.Alphanumeric);
    }
    internal static string InputStatusText(System.Globalization.CultureInfo? language, ImeConversionModeValues mode) =>
        language?.TwoLetterISOLanguageName == "ko" && mode.HasFlag(ImeConversionModeValues.Native) ? "입력 한글" : "입력 영문";

    private void RefreshEditorStatus()
    {
        foreach (var refresh in ribbonRefresh) refresh();
        RefreshInputStatus();
        if (ConversionStatus is null || CaretStatus is null) return;
        var failed = session?.IsTextJob != true && session?.IsConversionFailed(DisplayedOriginalPage) == true;
        OriginalFailure.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        OriginalContent.Visibility = failed ? Visibility.Collapsed : Visibility.Visible;
        var noPanels = EmptyPanelActions?.Visibility == Visibility.Visible;
        var showFailedStatus = failed && !noPanels && zoomPanel == ZoomPanel.Original;
        FailedPageStatus.Visibility = showFailedStatus ? Visibility.Visible : Visibility.Collapsed;
        if (failed && session is { } owner)
        {
            FailureReasonText.Text = owner.PageFailureReasons.GetValueOrDefault(DisplayedOriginalPage) == "NETWORK_ERROR"
                ? "네트워크 오류로 이 쪽을 변환하지 못했습니다." : "이 쪽을 변환하지 못했습니다.";
            FailedPageStatusText.Text = $"{DisplayedOriginalPage}쪽 변환 실패";
            OriginalRetryButton.IsEnabled = owner.JobId is not null && !owner.IsReadOnly && owner.Progress is null
                && !owner.HasPendingRestart && !owner.HasPendingServerPages;
        }
        var progress = session?.Progress;
        ConversionStatus.Visibility = progress is null || showFailedStatus ? Visibility.Collapsed : Visibility.Visible;
        BlockStatus.Visibility = progress is null && !showFailedStatus && !noPanels ? Visibility.Visible : Visibility.Collapsed;
        var operation = progress?.IsProofreading == true ? "교열 중" : "변환 중";
        ConversionStatusText.Text = progress is null ? "" : progress.Total > 0
            ? $"{operation} {(progress.IsProofreading ? progress.Succeeded : progress.Finished)}/{progress.Total}쪽" : operation;
        if (ConversionStatus.ContextMenu is { IsOpen: true } menu && (progress is null || !ReferenceEquals(menu.Tag, session)))
            menu.IsOpen = false;
        StatusPage.Text = noPanels ? "패널 없음" : zoomPanel switch { ZoomPanel.Original => "원본 패널", ZoomPanel.Braille => "점자 패널", _ => "묵자 패널" };
        CaretStatus.Visibility = noPanels ? Visibility.Collapsed : Visibility.Visible;
        if (session?.ActiveBlock is not { } block)
        {
            CaretStatus.Text = StatusOptions.Text = "";
            StatusPage.ToolTip = null;
            if (OriginalPosition is not null) OriginalPosition.Text = "";
            if (SourcePosition is not null) SourcePosition.Text = "";
            return;
        }

        OptionsLabel.Text = $"옵션 2024 · {(session.Options.KoreanGrade1 ? "1급" : "2급")} · {session.Layout.Columns}×{session.Layout.Lines}";
        StatusOptions.Text = $"한글 {(session.Options.KoreanGrade1 ? 1 : 2)}급 · 영어 {(session.Options.EnglishGrade1 ? 1 : 2)}급";
        StatusOptions.ToolTip = OptionsLabel.Text;
        if (noPanels)
        {
            CaretStatus.Text = "";
            StatusPage.ToolTip = CaretStatus.ToolTip = null;
            return;
        }
        var page = zoomPanel == ZoomPanel.Original ? DisplayedOriginalPage
            : PageEditor.Visibility == Visibility.Visible ? PageEditor.DisplayedPage : block.Page;
        var hasOriginal = session.IsTextJob != true && (session.OriginalPages.ContainsKey(page) || session.IsConversionFailed(page));
        var confirmedTotal = session.IsTextJob != true && session.TotalOriginalPages is { } known && page <= known ? known : (int?)null;

        StatusPage.ToolTip = session.IsTextJob == true ? $"텍스트 {page}쪽"
            : hasOriginal ? confirmedTotal is { } t ? $"원본 {page}쪽 (총 {t}쪽)" : $"원본 {page}쪽 (전체 쪽수 미확인)"
            : confirmedTotal is { } pendingTotal ? $"작업 {page}쪽 (총 {pendingTotal}쪽 · 원본 미수신)" : $"작업 {page}쪽 (원본 없음)";

        OriginalPosition.Text = session.IsTextJob == true || !failed && !session.OriginalPages.ContainsKey(DisplayedOriginalPage) ? ""
            : session.TotalOriginalPages is { } total && DisplayedOriginalPage <= total ? $"{DisplayedOriginalPage} / {total}쪽" : $"{DisplayedOriginalPage}쪽";
        OriginalPosition.ToolTip = OriginalPosition.Text;
        UpdateSourcePosition(block);
        CaretStatus.Text = zoomPanel switch
        {
            ZoomPanel.Original => OriginalPosition.Text,
            ZoomPanel.Braille => (detachedPane ?? Braille).LayoutLabel.Text,
            _ => SourcePosition.Text
        };
        CaretStatus.ToolTip = zoomPanel == ZoomPanel.Braille ? (detachedPane ?? Braille).GridView.DescribeCaret() : StatusPage.ToolTip;
        RefreshPanelPageBars();
    }

    private void UpdateSourcePosition(Semojum.Core.EditorBlock block)
    {
        int line, column;
        if (PageEditor.Visibility == Visibility.Visible)
        {
            if (!PageEditor.TryGetPageCaretPosition(PageEditor.DisplayedPage, out line, out column))
            { SourcePosition.Text = ""; SourcePosition.ToolTip = null; return; }
        }
        else
        {
            // The fallback editor holds one block rather than the whole page.
            if (SourceEditor.Text != block.Source)
            { SourcePosition.Text = ""; SourcePosition.ToolTip = null; return; }
            line = SourceEditor.TextArea.Caret.Line + session.Blocks
                .Where(b => b.Page == block.Page).TakeWhile(b => b != block)
                .Sum(b => b.Source.Count(c => c == '\n') + 1);
            column = SourceEditor.TextArea.Caret.Column;
        }
        SourcePosition.Text = $"{(PageEditor.Visibility == Visibility.Visible ? PageEditor.DisplayedPage : block.Page)}쪽 · {line}줄 · {column}칸";
        SourcePosition.ToolTip = SourcePosition.Text;
    }

    private void TextStylePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceEditor is null || TextStylePicker.SelectedIndex <= 0 || TextStylePicker.SelectedItem is not ComboBoxItem item) return;
        InsertTag(item.Content.ToString()!); TextStylePicker.SelectedIndex = 0;
    }
    private void LineNumbersToggle_Click(object sender, RoutedEventArgs e) => ApplyLineNumbers();
    private void ApplyLineNumbers()
    {
        SourceEditor.ShowLineNumbers = PageEditor.Editor.ShowLineNumbers = Braille.Editor.ShowLineNumbers = LineNumbersToggle.IsChecked;
        if (detachedPane is not null) detachedPane.Editor.ShowLineNumbers = LineNumbersToggle.IsChecked;
        if (LineNumbersToggle.IsChecked)
        {
            ApplyLineNumberAppearance(SourceEditor, appearance, currentLineNumberColor); ApplyLineNumberAppearance(PageEditor.Editor, appearance, currentLineNumberColor);
            ApplyLineNumberAppearance(Braille.Editor, appearance, currentLineNumberColor);
            if (detachedPane is not null) ApplyLineNumberAppearance(detachedPane.Editor, appearance, currentLineNumberColor);
        }
        Braille.GridView.ShowLineNumbers = LineNumbersToggle.IsChecked; Braille.GridView.InvalidateFaces();
        if (detachedPane is not null) { detachedPane.GridView.ShowLineNumbers = LineNumbersToggle.IsChecked; detachedPane.GridView.InvalidateFaces(); }
    }

    internal void HidePanel(string action)
    {
        switch (action)
        {
            case "hideOriginal":
                if (detachedPanels.TryGetValue(OriginalPanel, out var original)) original.Close();
                OriginalToggle.IsChecked = false; break;
            case "hideSource":
                if (detachedPanels.TryGetValue(SourcePanel, out var source)) source.Close();
                SourceToggle.IsChecked = false; break;
            case "hideBraille":
                detachedWindow?.Close(); BrailleToggle.IsChecked = false; break;
        }
        UpdatePanelLayout();
    }

    private void PanelToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender == SourceToggle && detachedPanels.TryGetValue(SourcePanel, out var source)) source.Close();
        if (sender == BrailleToggle) detachedWindow?.Close();
        UpdatePanelLayout();
    }
}
