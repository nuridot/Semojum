using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Feature.Board;
using Semojum.Feature.Editor;

namespace Semojum.Desktop;

public partial class MainWindow
{
    private readonly List<Action> ribbonRefresh = [];
    private sealed record TagChoice(string Label, string Tag, string? Preview = null);
    private readonly Dictionary<string, (string Group, int Index)> ribbonTagPositions = new(StringComparer.Ordinal);
    private Button? ribbonDrafts;
    private int notificationRequest;

    private void InitializeRibbon()
    {
        var groups = new StackPanel();
        void Tab(string label)
        {
            // The 88-DIP groups are centered in the 87-DIP interior above the lower border.
            groups = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = new TranslateTransform(0, -0.5) };
            System.Windows.Documents.TextElement.SetFontWeight(groups, FontWeights.Normal);
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = groups };
            EditorRibbon.Items.Add(new TabItem { Header = label, Content = scroll, MinWidth = label switch { "홈" => 37, "도움말" => 61, _ => 49 } });
        }
        void Group(string label, params FrameworkElement[] columns) => GroupWithCaption(new TextBlock
            { Text = label, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12, Foreground = (Brush)FindResource("RibbonCaptionInk") }, columns);
        void GroupWithCaption(FrameworkElement caption, params FrameworkElement[] columns)
        {
            if (groups.Children.Count > 0)
                groups.Children.Add(new Border { Width = 1, Height = 64, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromRgb(234, 234, 234)) });
            var body = new StackPanel { Margin = new Thickness(8, 4, 8, 0) };
            var row = new StackPanel { Height = 68, Orientation = Orientation.Horizontal };
            foreach (var column in columns)
            {
                if (row.Children.Count > 0) column.Margin = new Thickness(4, 0, 0, 0);
                row.Children.Add(column);
            }
            body.Children.Add(row);
            body.Children.Add(caption);
            groups.Children.Add(body);
        }
        static StackPanel Column(params FrameworkElement[] items)
        {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            foreach (var item in items)
            {
                if (stack.Children.Count > 0) item.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(item);
            }
            return stack;
        }
        Button Command(string label, string action, string icon, double minWidth = 48, bool small = false)
        {
            var button = RibbonButton(label);
            button.Tag = action;
            button.Click += Action_Click;
            var asset = icon == "find" && !small ? "ribbon-find-24.svg" : "ribbon-" + icon + ".svg";
            var graphic = DraftIconAsset(asset, small ? 16 : 24);
            var panel = new StackPanel { Orientation = small ? Orientation.Horizontal : Orientation.Vertical, Height = small ? 20 : 60, UseLayoutRounding = false };
            TextOptions.SetTextHintingMode(panel, TextHintingMode.Animated);
            panel.Children.Add(graphic);
            panel.Children.Add(new Controls.RibbonLabelBox { Child = new TextBlock { Text = label, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight },
                Margin = small ? new(4, 0, 0, 0) : new(0, 4, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            button.Content = panel;
            button.Height = small ? 20 : 68;
            button.Padding = small ? new Thickness(4, 0, 8, 0) : new Thickness(4, 8, 4, 0);
            button.VerticalContentAlignment = small ? VerticalAlignment.Center : VerticalAlignment.Top;
            button.MinWidth = minWidth;
            if (small) button.HorizontalAlignment = button.HorizontalContentAlignment = HorizontalAlignment.Left;
            else button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            if (action == "drafts") ribbonDrafts = button;
            if (action.StartsWith("tag:") || action is "undo" or "redo" or "cut" or "paste" or "table" or "memo" or "math" or "symbol" or "headerInsert" or "footerInsert" or "footer" or "layout")
                ribbonRefresh.Add(() => button.IsEnabled = session is { IsReadOnly: false });
            return button;
        }
        Button Small(string label, string action, string icon, double minWidth) => Command(label, action, icon, minWidth, small: true);

        Tab("파일");
        Group("문서", Command("새 문서", "new", "new"), Command("열기", "open", "open"), Command("저장", "save", "save"),
            Column(Small("다른 이름으로 저장", "saveAs", "saveas", 121), Small("범위 지정 저장", "export", "rangesave", 101), Small("문서 닫기", "closeDocument", "close", 77)));
        Group("인쇄", Command("미리 보기", "composition", "zoom", 53), Command("인쇄", "print", "print"));
        Tab("홈");
        Group("편집", Command("되돌리기", "undo", "undo", 50), Command("다시 실행", "redo", "redo", 53),
            Column(Small("붙여넣기", "paste", "paste", 74), Small("잘라내기", "cut", "cut", 74), Small("복사", "copy", "copy", 53)));
        Group("찾기", Command("찾기·바꾸기", "find", "find", 63), Command("이동", "goto", "goto"));
        Group("메모", Command("메모 추가", "memo", "memo", 53), Column(Small("이전 메모", "previousMemo", "chevl", 77), Small("다음 메모", "nextMemo", "chevr", 77)));
        Tab("삽입");
        Group("나누기", Command("쪽바꿈", "tag:쪽바꿈", "pagebreak"), Command("장바꿈", "tag:장바꿈", "pagebreak"),
            Column(Small("묵자 쪽번호", "tag:묵자쪽번호", "pageno", 87), Small("점자 면번호", "tag:점자면번호", "numsq", 87)));
        Group("글자", Column(TagChoices("정렬", new("왼쪽", "왼쪽정렬"), new("가운데", "가운데정렬"), new("오른쪽", "오른쪽정렬")),
            TagChoices("강조", new("밑줄", "강조", "⠠⠤⠀⠤⠄"), new("굵게", "굵은", "⠰⠤⠀⠤⠆"), new("색상/모양", "색강조", "⠐⠤⠀⠤⠂"), new("취소선", "취소선", "⠈⠤⠀⠤⠁")),
            TagChoices("빈칸", new("밑줄 빈칸", "밑줄빈칸", "⠸⠤"), new("네모 빈칸", "네모빈칸", "⠸⠦⠀⠴⠇"))));
        Group("묶음", Command("점역자 주", "tag:주", "note", 53), Column(TagChoices("글상자", new("1단계", "상자"), new("2단계", "상자2"), new("3단계", "상자3"))));
        Group("점자 줄", Column(TagChoices("점자 줄", new("상단 줄", "상단줄", "⠉⠉⠉⠉⠉⠉"), new("중단 줄", "중단줄", "⠒⠒⠒⠒⠒⠒"), new("하단 줄", "하단줄", "⠤⠤⠤⠤⠤⠤"), new("상단 굵은 줄", "상단굵은줄", "⠛⠛⠛⠛⠛⠛"), new("하단 굵은 줄", "하단굵은줄", "⠶⠶⠶⠶⠶⠶")),
            TagChoices("표 테두리", new("표 시작", "표시작", "⠖⠒⠒⠒⠲"), new("표 중앙", "표중앙", "⠗⠒⠒⠒⠺"), new("표 끝", "표끝", "⠓⠒⠒⠒⠚"), new("표 구분선", "표구분선", "⠐⠐⠐⠐⠐"), new("표지 시작", "표지시작", "⠿⠛⠛⠛⠿"), new("표지 세로", "표지세로", "⠿⠀⠀⠀⠿"), new("표지 끝", "표지끝", "⠿⠶⠶⠶⠿"))));
        Group("넣기", Command("수식", "math", "formula"), Command("표", "table", "table"));
        Group("시각자료", Command("대체 초안", "drafts", "image", 53), Column(Small("대체 초안 유형", "visualDescriptions", "imgedit", 101)));
        Group("기호", Column(Small("특수 기호", "symbol", "symbols", 77), Small("상용구", "snippets", "snippet", 64)));
        Group("머리말·꼬리말", Command("머리말", "headerInsert", "header"), Command("꼬리말", "footerInsert", "footer"), Column(Small("꼬리말 관리", "footer", "headfoot", 87)));
        Tab("점역");
        Group("옵션", Command("점역 옵션", "defaults", "set", 53), Column(
            OptionChoices("한글", ["1급(정자)", "2급(약자)"], o => o.KoreanGrade1 ? 0 : 1, (o, i) => o with { KoreanGrade1 = i == 0 }),
            OptionChoices("영어", ["1급(정자)", "2급(약자)"], o => o.EnglishGrade1 ? 0 : 1, (o, i) => o with { EnglishGrade1 = i == 0 }),
            OptionChoices("줄바꿈 단위", ["음절", "어절"], o => o.LineBreakUnit == "word" ? 1 : 0, (o, i) => o with { LineBreakUnit = i == 0 ? "syllable" : "word" })));
        Group("조판", Column(LayoutNumber(true), LayoutNumber(false), Small("조판 설정", "layout", "margins", 77)));
        Group("규정", Command("점역 규정", "rulebook", "rulebook", 53), Command("적용 규정", "rules", "proof", 53));
        Group("설정", Column(Small("6점자 키설정", "sixdot", "keyboard", 95)));
        Tab("보기");
        Group("패널", Column(RibbonToggle("원본", OriginalToggle), RibbonToggle("묵자", SourceToggle), RibbonToggle("점자", BrailleToggle)));
        Group("표시", Column(RibbonToggle("줄 번호", LineNumbersToggle, "ribbon-lineno-16"), RibbonToggle("조판부호", FormattingToggle, "ribbon-para"), RibbonToggle("쪽 분리선", OutlineToggle, "ribbon-borderr")),
            Column(RibbonToggle("메모", MemoToggle, "ribbon-memo-16"), RibbonToggle("검토 표시", ReviewToggle, "ribbon-important"), RibbonToggle("줄 점역", LinePreviewToggle, "ribbon-translate")));
        RibbonHeaderActions.Children.Remove(AdditionalCommands);
        GroupWithCaption(AdditionalCommands, Column(RibbonToggle("상태 표시줄", StatusToggle, "ribbon-panelbtm"), Small("글꼴·색", "colors", "font", 67)));
        var allCommands = new MenuItem { Header = "전체 명령", IsCheckable = true };
        allCommands.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(ToggleButton.IsChecked))
            { Source = AdditionalCommands, Mode = BindingMode.TwoWay });
        EditorRibbon.ContextMenu = new ContextMenu { Items = { allCommands } };
        Tab("도움말");
        Group("도움말", Command("도움말", "help", "help"));
        Group("게시판", Column(Small("공지", "board", "notice", 53), Small("문의", "inquiry", "mail", 53)));
        Group("정보", Column(Small("누리닷 정보", "about", "info", 87), RibbonToggle("자동 업데이트", AutoUpdateToggle, "ribbon-sync")));
        EditorRibbon.SelectedIndex = 1;
        // On a short work area the expanded command list and ribbon otherwise consume the full editor height.
        void FitCommandLists()
        {
            bool narrow = ActualWidth is > 0 and < 950;
            Grid.SetRow(RibbonHeaderActions, narrow ? 0 : 1);
            RuleSearch.Width = narrow ? Math.Clamp(ActualWidth - 300, 100, 200) : 200;
            EditorRibbon.MaxHeight = ActualHeight is > 0 and < 500 && AdditionalCommands.IsChecked == true
                ? 32 : double.PositiveInfinity;
        }
        SizeChanged += (_, _) => FitCommandLists();
        AdditionalCommands.Checked += (_, _) => FitCommandLists();
        AdditionalCommands.Unchecked += (_, _) => FitCommandLists();
        EditorRibbon.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, EditorRibbon) && ActualHeight is > 0 and < 500)
                AdditionalCommands.IsChecked = false;
        };
    }

    private static Button RibbonButton(string label)
    {
        var button = new Button { MinWidth = 0, MinHeight = 0, Height = 20, FontSize = 12, FontWeight = FontWeights.Normal, Padding = new(4, 0, 4, 0),
            Margin = new(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = label };
        AutomationProperties.SetName(button, label);
        return button;
    }

    private CheckBox RibbonToggle(string label, MenuItem target, string? icon = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon is not null)
        {
            var image = DraftIconAsset(icon + ".svg", 16);
            image.Margin = new Thickness(0, 0, 4, 0);
            content.Children.Add(image);
        }
        content.Children.Add(new Controls.RibbonLabelBox { Child = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } });
        var toggle = new CheckBox { Content = content, Height = 20, MinHeight = 0, Margin = new Thickness(0), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left,
            Template = (ControlTemplate)FindResource("RibbonToggleTemplate") };
        AutomationProperties.SetName(toggle, label);
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(target.IsChecked)) { Source = target, Mode = BindingMode.OneWay });
        toggle.Click += (_, _) =>
        {
            target.IsChecked = toggle.IsChecked == true;
            target.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, target));
        };
        return toggle;
    }

    private Button TagChoices(string title, params TagChoice[] tags)
    {
        for (var i = 0; i < tags.Length; i++) ribbonTagPositions.Add(tags[i].Tag, (title, i));
        return RibbonChoices(title, tags.Select(t => t.Label).ToArray(),
            owner => owner.TagSelections.GetValueOrDefault(title),
            (_, index) => ExecuteAsync("tag:" + tags[index].Tag), tags.Select(t => t.Preview).ToArray());
    }

    private Button OptionChoices(string title, string[] labels, Func<BrailleOptions, int> current,
        Func<BrailleOptions, int, BrailleOptions> set) =>
        RibbonChoices(title, labels, owner => current(owner.Model.Options), (owner, index) =>
        {
            CompleteSourceInput();
            owner.Model.Options = set(owner.Model.Options, index);
            return Task.CompletedTask;
        });

    private Button RibbonChoices(string title, string[] labels, Func<OpenDocument, int> current,
        Func<OpenDocument, int, Task> apply, string?[]? previews = null)
    {
        var button = RibbonButton(title);
        button.Template = (ControlTemplate)FindResource("RibbonChoiceButton");
        button.HorizontalAlignment = HorizontalAlignment.Left;
        void CloseMenu()
        {
            if (button.ContextMenu is { IsOpen: true } menu) menu.IsOpen = false;
        }
        void Refresh()
        {
            button.IsEnabled = openDocument?.Model.IsReadOnly == false;
            if (openDocument is { } owner)
            {
                var value = labels[current(owner)];
                button.Content = value;
                AutomationProperties.SetHelpText(button, value);
            }
            if (!button.IsEnabled || !ReferenceEquals(button.ContextMenu?.Tag, openDocument)) CloseMenu();
        }
        void Open()
        {
            var owner = openDocument;
            if (owner == null || owner.Model.IsReadOnly || button.ContextMenu is { IsOpen: true }) return;
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("RibbonChoiceMenu"), Tag = owner,
                HorizontalOffset = button.ActualWidth - 96 - 16, VerticalOffset = -4
            };
            var selected = current(owner);
            for (var i = 0; i < labels.Length; i++)
            {
                var index = i;
                var item = new MenuItem { Header = labels[i], Icon = previews?[i], IsCheckable = true, IsChecked = i == selected,
                    Style = (Style)FindResource("RibbonChoiceItem") };
                AutomationProperties.SetName(item, labels[i]);
                item.Click += async (_, _) =>
                {
                    if (openDocument != owner || owner.Model.IsReadOnly) return;
                    await apply(owner, index);
                    Refresh();
                };
                menu.Items.Add(item);
            }
            menu.Closed += (_, _) => { if (button.ContextMenu == menu) button.ContextMenu = null; };
            OpenRibbonMenu(button, menu);
        }
        button.Click += (_, _) => Open();
        button.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Down && !(e.Key == Key.System && e.SystemKey == Key.Down)) return;
            e.Handled = true; Open();
            if (button.ContextMenu is { IsOpen: true } menu)
                menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsChecked)?.Focus();
        };
        button.IsVisibleChanged += (_, _) => { if (!button.IsVisible) CloseMenu(); };
        ribbonRefresh.Add(Refresh);
        return button;
    }

    private FrameworkElement LayoutNumber(bool columns)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 20 };
        string label = columns ? "칸" : "줄";
        row.Children.Add(new Controls.RibbonLabelBox { Margin = new Thickness(0, 0, 8, 0), Child = new TextBlock
            { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = (Brush)FindResource("Muted") } });
        var controls = new DockPanel();
        var border = new Border { Width = 64, Height = 20, Background = Brushes.White, BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = controls };
        var arrows = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -1, 0, -1) };
        DockPanel.SetDock(arrows, Dock.Right);
        controls.Children.Add(arrows);
        var field = new TextBox { MinWidth = 0, MinHeight = 0, Height = 18, FontSize = 12, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            Padding = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
        controls.Children.Add(field);
        AutomationProperties.SetName(field, columns ? "줄당 칸 수" : "페이지당 줄 수");
        int Value() => columns ? session.Layout.Columns : session.Layout.Lines;
        void Refresh()
        {
            if (session is null) return;
            border.IsEnabled = !session.IsReadOnly;
            if (!field.IsKeyboardFocusWithin) field.Text = Value().ToString();
        }
        DocumentSession? editing = null;
        void Commit()
        {
            if (editing != session || session.IsReadOnly) return;
            if (int.TryParse(field.Text, out var value) && value >= (columns ? 8 : 4) && value <= 100)
                session.Layout = columns ? session.Layout with { Columns = value } : session.Layout with { Lines = value };
            else session.Status = columns ? "칸 수는 8–100으로 입력해 주세요." : "줄 수는 4–100으로 입력해 주세요.";
            field.Text = Value().ToString();
        }
        void Step(int delta)
        {
            if (session.IsReadOnly) return;
            var value = Math.Clamp(Value() + delta, columns ? 8 : 4, 100);
            session.Layout = columns ? session.Layout with { Columns = value } : session.Layout with { Lines = value };
            field.Text = value.ToString();
        }
        foreach (var delta in new[] { 1, -1 })
        {
            var arrow = DraftIconAsset(delta == 1 ? "ribbon-step-up.svg" : "ribbon-step-down.svg", 10);
            var step = RibbonButton(label + (delta == 1 ? " 늘리기" : " 줄이기"));
            step.Width = 16; step.Height = 10; step.Padding = new Thickness(0); step.Content = arrow;
            step.Click += (_, _) => Step(delta);
            arrows.Children.Add(step);
        }
        field.GotKeyboardFocus += (_, _) => editing = session;
        field.LostKeyboardFocus += (_, _) => Commit();
        field.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            if (e.Key is Key.Up or Key.Down) { Commit(); Step(e.Key == Key.Up ? 1 : -1); e.Handled = true; }
        };
        ribbonRefresh.Add(Refresh);
        row.Children.Add(border);
        return row;
    }

    private static void OpenRibbonMenu(FrameworkElement anchor, ContextMenu menu)
    {
        anchor.ContextMenu = menu;
        menu.PlacementTarget = anchor;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async void AccountOpen_Click(object sender, RoutedEventArgs e) => await ExecuteAsync(api.Account is null ? "login" : "usage");

    private void RibbonAccount_Click(object sender, RoutedEventArgs e)
    {
        if (api.Account is not { } account)
        {
            var login = new MenuItem { Header = "로그인", Tag = "login" };
            login.Click += Action_Click;
            var signedOut = new ContextMenu();
            signedOut.Items.Add(login);
            OpenRibbonMenu(AccountMenuButton, signedOut);
            return;
        }
        var (menu, page, logout) = Dialogs.AccountContextMenu.Create(AccountMenuButton, account, account.DisplayName);
        page.Tag = "usage"; page.Click += Action_Click;
        logout.Tag = "logout"; logout.Click += Action_Click;
        OpenRibbonMenu(AccountMenuButton, menu);
    }

    private async void RibbonNotifications_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync("notifications");
        await RefreshUnreadAsync();
    }

    private async Task RefreshUnreadAsync()
    {
        var request = ++notificationRequest;
        var account = api.Account;
        RibbonUnread.Visibility = Visibility.Collapsed;
        if (account is null || resourcesReleased) return;
        try
        {
            var count = (await new BoardClient(api).NotificationsAsync()).UnreadCount;
            if (request != notificationRequest || api.Account != account || resourcesReleased) return;
            RibbonUnread.Text = count.ToString();
            RibbonUnread.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.IO.InvalidDataException or Semojum.Core.Integration.SemojumApiException)
        { System.Diagnostics.Debug.WriteLine("알림 수를 조회하지 못했습니다."); }
    }

    private async Task MoveToMemoAsync(bool next)
    {
        var locations = new List<NavigationLocation>();
        foreach (var block in session.Blocks)
            foreach (var memo in block.Memos.Where(m => !m.Resolved).OrderBy(m => m.LineIndex))
            {
                var panel = memo.Panel == "BRAILLE" ? SearchPanel.Braille : SearchPanel.Text;
                var offsets = DocumentNavigation.LineOffsets(DocumentSearch.Content(block, panel));
                if (memo.LineIndex >= 0 && memo.LineIndex < offsets.Length)
                    locations.Add(new(block, panel, offsets[memo.LineIndex]));
            }
        if (locations.Count == 0) { session.Status = "이동할 메모가 없습니다."; return; }
        var current = session.ActiveBlock;
        var index = current is null ? -1 : session.Blocks.IndexOf(current);
        var caret = SearchEditor(ActiveSearchPanel).CaretOffset;
        var target = next
            ? locations.FirstOrDefault(n => session.Blocks.IndexOf(n.Block) > index || n.Block == current && n.Offset > caret) ?? locations[0]
            : locations.LastOrDefault(n => session.Blocks.IndexOf(n.Block) < index || n.Block == current && n.Offset < caret) ?? locations[^1];
        await NavigateToAsync(target);
    }
}
