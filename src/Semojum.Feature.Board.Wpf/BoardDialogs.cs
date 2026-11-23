using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Automation;
using Microsoft.Win32;
using Semojum.Core.Integration;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

internal static class BoardDesign
{
    internal static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    internal static SvgViewbox Icon(string name) => new() { Source = new Uri($"pack://application:,,,/Semojum.Feature.Board.Wpf;component/Assets/Figma/board-{name}.svg"), Width = 16, Height = 16, Stretch = Stretch.None };
    internal static void Frame(Window window)
    {
        if (window.Content is not Grid frame || frame.Children[0] is not Border { Child: DockPanel heading }) return;
        if (heading.Children.OfType<StackPanel>().FirstOrDefault() is { } buttons)
        {
            buttons.Margin = new(0);
            foreach (var (button, asset) in buttons.Children.OfType<Button>().Zip(new[] { "caption-min", "caption-max", "caption-close" }))
                button.Content = Icon(asset);
        }
        window.Content = null;
        window.Content = new Border { BorderBrush = Brush("#D1D1D1"), BorderThickness = new(1), CornerRadius = new(8), Child = frame };
    }
    internal static Button Navigation(string label, double width, Style style, bool selected, Action action)
    {
        var button = new Button { Content = label, Width = width, Style = style };
        SetNavigationSelected(button, selected);
        button.Click += (_, _) => action();
        return button;
    }
    internal static void SetNavigationSelected(Button button, bool selected)
    {
        button.Tag = selected ? "selected" : null;
        button.Foreground = Brush(selected ? "#003E92" : "#5F5F5F");
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        AutomationProperties.SetItemStatus(button, selected ? "선택됨" : "선택 안 됨");
    }
    internal static Style CheckStyle() => PublicDialogDesign.ConsentStyle("Semojum.Feature.Board.Wpf", "board-check.svg", 16);
}

public sealed class BoardDialog : CompactDialog
{
    private sealed record OpenBody(BoardBodyView View, UIElement[] Covered, Func<bool, Task> Returned);
    private readonly List<OpenBody> bodies = [];
    // The tab widths match the G1 board list.
    private static readonly (string Label, BoardListKind Kind, double Width)[] Lists =
        [("전체 글", BoardListKind.All, 82), ("공지", BoardListKind.Notices, 60), ("내 글", BoardListKind.Mine, 68), ("답변 받은 글", BoardListKind.Answered, 118)];
    private readonly DockPanel body;
    private readonly BoardWorkspace workspace;
    private readonly BoardListView grid;
    /// <summary>True once a post was saved or deleted in this window.</summary>
    public bool Changed { get; private set; }
    public BoardDialog(BoardClient client, string workInfo, bool initiallyMine = false) : this(client, workInfo, initiallyMine, null) { }
    public static BoardDialog Notices(BoardClient client, string workInfo) => new(client, workInfo, false, null, BoardListKind.Notices);
    /// <summary>Opens the board at one post; 「목록」 then shows the list in the same window.</summary>
    public static BoardDialog Reading(BoardClient client, BoardPost post, string workInfo, bool initiallyMine = false) => new(client, workInfo, initiallyMine, board => new PostView(board, client, post, workInfo));
    /// <summary>Opens the board at 글쓰기; 「등록」 or 「취소」 then shows the list in the same window.</summary>
    public static BoardDialog Writing(BoardClient client, string workInfo, bool initiallyMine = false) => new(client, workInfo, initiallyMine, board => new PostEditorView(board, client, null, workInfo));
    private BoardDialog(BoardClient client, string workInfo, bool initiallyMine, Func<BoardDialog, BoardBodyView>? start, BoardListKind? initialList = null) : base("게시판 - 누리닷", 1440, 1000, editor: true, editorActions: true)
    {
        Background = Brushes.White;
        grid = new BoardListView((Style)FindResource("BoardListGrid"));
        var previousFooter = (Border)Actions.Parent; previousFooter.Child = null; previousFooter.Visibility = Visibility.Collapsed;
        BoardDesign.Frame(this);
        workspace = new BoardWorkspace(this, client, workInfo);
        Root.Children.Add(workspace);
        var root = body = workspace.Page;
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        var tabBar = new Border { BorderBrush = BoardDesign.Brush("#D1D1D1"), BorderThickness = new(0,0,0,1), Child = tabs };
        DockPanel.SetDock(tabBar, Dock.Top); root.Children.Add(tabBar);
        var count = new TextBlock { FontSize = 12, Height = 16, Foreground = BoardDesign.Brush("#5F5F5F"), Margin = new(0,16,0,16) };
        DockPanel.SetDock(count, Dock.Top); root.Children.Add(count);
        var table = new Border { BorderBrush = BoardDesign.Brush("#1A1A1A"), BorderThickness = new(0,2,0,0), Child = grid };
        DockPanel.SetDock(table, Dock.Top); root.Children.Add(table);
        Actions.HorizontalAlignment = HorizontalAlignment.Left; Actions.Margin = new(0);
        var fields = Choices(["제목+내용", "제목", "내용", "글쓴이"], "제목+내용"); fields.Width = 140; fields.Height = fields.MinHeight = 32; fields.Padding = new(8, 0, 25, 0);
        var query = new TextBox { Width = 280, Height = 32, MinHeight = 32, MaxLength = 100, Margin = new(8, 0, 0, 0), Padding = new(7, 0, 7, 0) };
        System.Windows.Automation.AutomationProperties.SetName(query, "게시판 검색어");
        var fieldsView = new Grid { Width = 140, Height = 32 }; fieldsView.Children.Add(fields); fieldsView.Children.Add(new Border { Width = 24, Margin = new(0, 1, 1, 1), Background = BoardDesign.Brush("#FBFBFB"), HorizontalAlignment = HorizontalAlignment.Right, IsHitTestVisible = false });
        var chevron = BoardDesign.Icon("chevron-down"); chevron.HorizontalAlignment = HorizontalAlignment.Right; chevron.Margin = new(0, 0, 5, 0); chevron.IsHitTestVisible = false; fieldsView.Children.Add(chevron);
        var queryField = new Grid { Margin = new(8,0,0,0) };
        query.Margin = new(0); query.Background = Brushes.White; query.Padding = new(12,0,12,0);
        queryField.Children.Add(query);
        var hint = new TextBlock { Text = "검색어", Margin = new(13,0,0,0), IsHitTestVisible = false, Foreground = BoardDesign.Brush("#8A8A8A") };
        queryField.Children.Add(hint);
        query.TextChanged += (_, _) => hint.Visibility = query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Actions.Children.Add(fieldsView); Actions.Children.Add(queryField);
        var list = new BoardListViewModel(client, initiallyMine);
        var pageInput = new TextBox { Width = 44, Height = 24, MinHeight = 0, Padding = new(8, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(pageInput, "게시판 쪽 번호");
        var pageTotal = new TextBlock { Foreground = BoardDesign.Brush("#5F5F5F"), Margin = new(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var numberButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var jump = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
        jump.Children.Add(pageInput); jump.Children.Add(pageTotal);
        void ShowJump()
        {
            jump.Visibility = Visibility.Visible; numberButtons.Visibility = Visibility.Collapsed;
            pageInput.Focus(); pageInput.SelectAll();
        }
        void HideJump() { jump.Visibility = Visibility.Collapsed; numberButtons.Visibility = Visibility.Visible; }
        Button? previous = null, next = null;
        int TotalPages() => list.TotalPages;
        // A delayed list request must not trap the user in the dialog. Other
        // operations keep the shared busy guard to protect pending edits.
        Closing += (_, e) => { if (list.IsLoading) { e.Cancel = false; Lifetime.Cancel(); } };
        Closing += (_, e) => { if (!e.Cancel && !ConfirmLeaveBodies()) e.Cancel = true; };
        async Task LoadAsync(int? targetPage = null, BoardListKind? targetKind = null)
        {
            var field = fields.SelectedIndex switch { 1 => "TITLE", 2 => "CONTENT", 3 => "AUTHOR", _ => "TITLE_CONTENT" };
            await list.LoadAsync(targetPage ?? list.Page, targetKind ?? list.Kind, field, query.Text, Lifetime.Token);
            if (Lifetime.IsCancellationRequested) return;
            grid.ItemsSource = list.Rows;
            count.Text = $"{(list.Kind == BoardListKind.All ? "전체" : Lists.Single(l => l.Kind == list.Kind).Label)} {list.TotalCount}건";
            for (var i = 0; i < tabs.Children.Count; i++) BoardDesign.SetNavigationSelected((Button)tabs.Children[i], Lists[i].Kind == list.Kind);
            RenderPages();
        }
        async Task GoPageAsync(int target)
        {
            if (target < 1 || target > TotalPages()) { Status.Text = $"1부터 {TotalPages()}까지의 쪽 번호를 입력해 주세요."; RenderPages(); return; }
            try { await LoadAsync(target); }
            catch { RenderPages(); throw; }
        }
        async Task SwitchListAsync(BoardListKind targetKind)
        {
            try { await LoadAsync(1, targetKind); }
            catch { RenderPages(); throw; }
        }
        void RenderPages()
        {
            pageInput.Text = list.Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
            pageTotal.Text = "/ " + TotalPages().ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (previous is not null) previous.IsEnabled = list.Page > 1;
            if (next is not null) next.IsEnabled = list.Page < TotalPages();
            numberButtons.Children.Clear();
            var first = (list.Page - 1) / 10 * 10 + 1;
            for (var n = first; n <= Math.Min(TotalPages(), first + 9); n++)
            {
                var target = n;
                var button = Button(n.ToString(System.Globalization.CultureInfo.InvariantCulture), async () => { if (target == list.Page) ShowJump(); else await RunAsync(() => GoPageAsync(target)); });
                PublicDialogDesign.StyleAction(button, target == list.Page);
                button.Width = button.Height = 32; button.Padding = new(0); button.Margin = new(0,0,4,0);
                if (target != list.Page) button.Background = button.BorderBrush = Brushes.Transparent;
                else button.ToolTip = "현재 쪽 · 쪽 번호로 이동";
                System.Windows.Automation.AutomationProperties.SetName(button, $"게시판 {n}쪽");
                numberButtons.Children.Add(button);
            }
        }
        async Task OpenAsync()
        {
            if (grid.SelectedItem is not BoardRow row) return;
            if (row.NoticeId is { } notice)
            {
                var n = await client.NoticeAsync(notice, Lifetime.Token);
                BoardNoticeDialog.ShowFor(this, n);
                await LoadAsync();
            }
            else ShowBody(new PostView(this, client, await client.ReadAsync(row.PostId, Lifetime.Token), workInfo, list.Rows), _ => LoadAsync());
        }
        grid.MouseDoubleClick += async (_, _) => await RunAsync(OpenAsync);
        var search = Action("검색", async () => await GoPageAsync(1));
        PublicDialogDesign.StyleAction(search); search.Width = 88; search.Margin = new(8,0,0,0);
        query.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await RunAsync(() => GoPageAsync(1)); }
        };
        var write = Button("글쓰기", async () => await RunAsync(() =>
        {
            ShowBody(new PostEditorView(this, client, null, workInfo), saved => saved ? GoPageAsync(1) : Task.CompletedTask);
            return Task.CompletedTask;
        }), true);
        PublicDialogDesign.StyleAction(write, true);
        write.Width = 88; write.Margin = new(8,0,0,0); write.IsDefault = false;
        foreach (var (label, kind, width) in Lists)
            tabs.Children.Add(BoardDesign.Navigation(label, width, (Style)workspace.FindResource("BoardTab"), kind == list.Kind, async () => { if (LeaveBodies()) await RunAsync(() => SwitchListAsync(kind)); }));
        // Paging and notifications remain accessible when the window is resized.
        var navigation = new StackPanel { Orientation = Orientation.Horizontal }; var pageMenu = new ContextMenu(); pageMenu.Items.Add(new MenuItem { Header = navigation, StaysOpenOnClick = true }); grid.ContextMenu = pageMenu;
        count.ToolTip = "이전·다음 쪽: 목록에서 마우스 오른쪽 단추 또는 PageUp·PageDown";
        navigation.Children.Add(Button("이전", async () => await RunAsync(async () => { if (list.Page > 1) await GoPageAsync(list.Page - 1); })));
        navigation.Children.Add(Button("다음", async () => await RunAsync(async () => { if (list.Page < TotalPages()) await GoPageAsync(list.Page + 1); })));
        var jumpMenu = new MenuItem { Header = "쪽 번호로 이동", InputGestureText = "Ctrl+G" };
        jumpMenu.Click += (_, _) => ShowJump(); pageMenu.Items.Add(jumpMenu);
        previous = Button("이전", async () => await RunAsync(() => GoPageAsync(list.Page - 1)));
        previous.Content = BoardDesign.Icon("chevron-left"); previous.ToolTip = "이전 쪽"; System.Windows.Automation.AutomationProperties.SetName(previous, "이전 쪽"); PublicDialogDesign.StyleAction(previous); previous.Width = previous.Height = 32; previous.Background = previous.BorderBrush = Brushes.Transparent; previous.Margin = new(0,0,4,0);
        next = Button("다음", async () => await RunAsync(() => GoPageAsync(list.Page + 1)));
        next.Content = BoardDesign.Icon("chevron-right"); next.ToolTip = "다음 쪽"; System.Windows.Automation.AutomationProperties.SetName(next, "다음 쪽"); PublicDialogDesign.StyleAction(next); next.Width = next.Height = 32; next.Background = next.BorderBrush = Brushes.Transparent; next.Margin = new(0);
        var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        pager.Children.Add(previous); pager.Children.Add(jump); pager.Children.Add(numberButtons); pager.Children.Add(next);
        var pagingArea = new ScrollViewer { Height = 40, Margin = new(0,16,0,0), Padding = new(0,8,0,0), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = pager };
        DockPanel.SetDock(pagingArea, Dock.Top); root.Children.Add(pagingArea);
        var footer = new DockPanel { MinWidth = 630, LastChildFill = false };
        DockPanel.SetDock(Actions, Dock.Left); footer.Children.Add(Actions);
        DockPanel.SetDock(write, Dock.Right); footer.Children.Add(write);
        var footerScroll = new ScrollViewer { Content = footer, Margin = new(0,16,0,0), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AutomationProperties.SetName(footerScroll, "게시판 검색 및 쪽 이동");
        root.Children.Add(footerScroll);
        var navigationMenu = new ContextMenu();
        foreach (var (label, kind, _) in Lists)
        {
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) => { if (LeaveBodies()) await RunAsync(() => SwitchListAsync(kind)); };
            navigationMenu.Items.Add(item);
        }
        navigationMenu.Items.Add(new Separator());
        var help = new MenuItem { Header = "도움말" };
        help.Click += (_, _) => Services.CommonWindowServices.ShowHelp(this, "게시판");
        navigationMenu.Items.Add(help);
        workspace.Back.ContextMenu = navigationMenu;
        pageInput.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; HideJump(); grid.Focus(); return; }
            if (e.Key != System.Windows.Input.Key.Enter) return;
            e.Handled = true;
            if (!int.TryParse(pageInput.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var target))
            { Status.Text = "쪽 번호를 숫자로 입력해 주세요."; RenderPages(); return; }
            await RunAsync(() => GoPageAsync(target));
            if (list.Page == target) { HideJump(); grid.Focus(); }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (bodies.Count == 0 && e.Key == System.Windows.Input.Key.G && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            { e.Handled = true; ShowJump(); }
        };
        grid.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await RunAsync(OpenAsync); }
            else if (e.Key is System.Windows.Input.Key.PageDown or System.Windows.Input.Key.PageUp)
            { e.Handled = true; await RunAsync(async () => { if (e.Key == System.Windows.Input.Key.PageDown && list.Page < TotalPages()) await GoPageAsync(list.Page + 1); else if (e.Key == System.Windows.Input.Key.PageUp && list.Page > 1) await GoPageAsync(list.Page - 1); }); }
        };
        // Opened at a post or 글쓰기, the list loads when that view is left.
        if (start is not null) ShowBody(start(this), _ => LoadAsync());
        OnLoad(() => bodies.Count == 0 ? LoadAsync(targetKind: initialList) : Task.CompletedTask);
    }
    internal Task RunBodyAsync(Func<Task> action) => RunAsync(action);
    internal CancellationToken Token => Lifetime.Token;
    internal void ScrollBodyToTop() => workspace.PageScroll.ScrollToHome();
    internal void Report(string message) => Status.Text = message;
    internal void ShowBody(BoardBodyView view, Func<bool, Task> returned)
    {
        var covered = body.Children.Cast<UIElement>().Where(c => c.Visibility == Visibility.Visible).ToArray();
        foreach (var element in covered) element.Visibility = Visibility.Collapsed;
        bodies.Add(new(view, covered, returned)); body.Children.Add(view);
        workspace.PageScroll.ScrollToHome();
        RoutedEventHandler? shown = null; shown = (_, _) => { view.Loaded -= shown; FocusBody(); }; view.Loaded += shown;
    }
    internal async Task LeaveBodyAsync(BoardBodyView view, bool changed)
    {
        if (bodies.Count == 0 || bodies[^1].View != view) return;
        var left = bodies[^1]; CloseTopBody(); Changed |= changed;
        try { await left.Returned(changed); }
        finally { FocusBody(); }
    }
    private void CloseTopBody()
    {
        var top = bodies[^1]; bodies.RemoveAt(bodies.Count - 1); body.Children.Remove(top.View);
        foreach (var element in top.Covered) element.Visibility = Visibility.Visible;
        workspace.PageScroll.ScrollToHome();
    }
    // The tabs and closing the window ask each open view, newest first, before typed content is lost.
    private bool ConfirmLeaveBodies()
    {
        for (var i = bodies.Count - 1; i >= 0; i--) if (!bodies[i].View.ConfirmLeave()) return false;
        return true;
    }
    private bool LeaveBodies()
    {
        if (bodies.Count == 0) return true;
        if (!ConfirmLeaveBodies()) return false;
        while (bodies.Count > 0) CloseTopBody();
        FocusBody(); return true;
    }
    // Keyboard focus moves to the visible view, or back to the list, once a running action has enabled the window again.
    private void FocusBody()
    {
        if (Root.IsEnabled)
        {
            Dispatcher.InvokeAsync(() => { if (!Root.IsEnabled) FocusBody(); else if (bodies.Count > 0) bodies[^1].View.FocusFirst(); else grid.Focus(); },
                System.Windows.Threading.DispatcherPriority.Input);
            return;
        }
        DependencyPropertyChangedEventHandler? enabled = null;
        enabled = (_, _) => { if (!Root.IsEnabled) return; Root.IsEnabledChanged -= enabled; FocusBody(); };
        Root.IsEnabledChanged += enabled;
    }
}

/// <summary>A board screen that takes the board body's place; leaving it shows what it covered again.</summary>
internal abstract class BoardBodyView : DockPanel
{
    protected BoardBodyView(BoardDialog board) => Board = board;
    protected BoardDialog Board { get; }
    /// <summary>False keeps this view open, for example when the user keeps writing.</summary>
    internal virtual bool ConfirmLeave() => true;
    internal abstract void FocusFirst();
    // Runs under the board's busy guard and reports failures in its status line, like the list's own actions.
    protected Button Action(Panel actions, string label, Func<Task> action, bool primary = false)
    { var button = CompactDialog.Button(label, () => Board.RunBodyAsync(action), primary); actions.Children.Add(button); return button; }
}

/// <summary>Figma D8 §8-2 글쓰기 (and 글 수정) in the board body.</summary>
internal sealed class PostEditorView : BoardBodyView
{
    private sealed record AttachmentChoice(string Name, long? ExistingId, string? LocalPath) { public override string ToString() => Name; }
    private readonly TextBox title;
    private readonly Func<bool> drafted;
    public BoardPost? SavedPost { get; private set; }
    public long SavedId { get; private set; }
    internal PostEditorView(BoardDialog board, BoardClient client, BoardPost? post, string workInfo) : base(board)
    {
        var name = post is null ? "글쓰기" : "글 수정"; AutomationProperties.SetName(this, name);
        var form = new BoardWriteForm(name, post);
        Children.Add(form);
        var actions = form.Actions;
        title = form.TitleInput;
        var content = form.ContentInput;
        var secret = form.Secret;
        var info = form.IncludeWork;
        var initialCategory = form.SelectedCategory;
        var attachments = new ObservableCollection<AttachmentChoice>((post?.Attachments ?? []).Select(a => new AttachmentChoice(a.FileName, a.AttachmentId, null)));
        var list = form.FileList;
        list.ItemsSource = attachments;
        list.Visibility = attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Semojum.Desktop.Controls.AttachmentPresentation.Configure<AttachmentChoice>(list, file => attachments.Remove(file), "BoardRemoveAttachment");
        AutomationProperties.SetName(list, "첨부한 파일");
        attachments.CollectionChanged += (_, _) => list.Visibility = attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        form.ChooseFile.Click += (_, _) =>
        {
            var picker = new OpenFileDialog { Multiselect = true, Filter = "문서·이미지|*.jpg;*.jpeg;*.png;*.gif;*.pdf;*.hwp;*.hwpx;*.txt;*.doc;*.docx;*.xls;*.xlsx;*.brf;*.zip" };
            if (picker.ShowDialog(Board) == true)
            {
                if (attachments.Count + picker.FileNames.Length + (info.IsChecked == true ? 1 : 0) > 5) { Board.Report("작업 정보 포함 최대 5개까지 첨부할 수 있습니다."); return; }
                foreach (var path in picker.FileNames)
                { if (new FileInfo(path).Length > 10 * 1024 * 1024) { Board.Report("한 파일은 10 MB까지입니다: " + Path.GetFileName(path)); continue; } attachments.Add(new(Path.GetFileName(path), null, path)); }
            }
        };
        var attachmentMenu = new ContextMenu(); var remove = new MenuItem { Header = "선택 첨부 제거" }; attachmentMenu.Items.Add(remove); list.ContextMenu = attachmentMenu;
        remove.Click += (_, _) => { if (list.SelectedItem is AttachmentChoice a) attachments.Remove(a); };
        list.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Delete && list.SelectedItem is AttachmentChoice a) { attachments.Remove(a); e.Handled = true; } };
        var infoMenu = new ContextMenu(); var infoPreview = new MenuItem { Header = "첨부할 작업 정보 보기" }; infoMenu.Items.Add(infoPreview); info.ContextMenu = infoMenu;
        infoPreview.Click += (_, _) => NoticeDialog.Choose(Board, "첨부할 작업 정보입니다.", workInfo, ["확인"], height: 316);
        drafted = () => title.Text != (post?.Title ?? "") || content.Text != (post?.Content ?? "") || (secret.IsChecked == true) != (post?.Secret == true)
            || (info.IsChecked == true) != (post is null) || form.SelectedCategory != initialCategory || attachments.Count != (post?.Attachments ?? []).Length || attachments.Any(a => a.LocalPath is not null);
        actions.Children.Add(CompactDialog.Button("취소", async () => { if (ConfirmLeave()) await Board.RunBodyAsync(() => Board.LeaveBodyAsync(this, false)); }));
        Action(actions, post is null ? "등록" : "저장", async () =>
        {
            var files = new List<UploadFile>();
            foreach (var a in attachments.Where(a => a.LocalPath is not null))
            { if (new FileInfo(a.LocalPath!).Length > 10 * 1024 * 1024) throw new InvalidDataException("첨부 파일이 10 MB를 초과했습니다."); files.Add(new(a.Name, await File.ReadAllBytesAsync(a.LocalPath!, Board.Token))); }
            if (info.IsChecked == true) files.Add(new($"작업정보_{DateTime.Now:yyyy-MM-dd}.txt", Encoding.UTF8.GetBytes(workInfo)));
            var saved = await client.SaveAsync(post?.PostId, title.Text, content.Text, secret.IsChecked == true, attachments.Where(a => a.ExistingId.HasValue).Select(a => a.ExistingId!.Value).ToArray(), files.ToArray(), form.SelectedCategory, Board.Token);
            SavedId = saved.Id; SavedPost = saved.Post; await Board.LeaveBodyAsync(this, true);
        }, true);
        foreach (Button button in actions.Children)
        {
            PublicDialogDesign.StyleAction(button, button.IsDefault);
            button.Width = 88; button.Margin = new(button.IsDefault ? 8 : 0, 0, 0, 0);
        }
    }
    // Same wording as the public inquiry draft guard (PublicDialogs.cs).
    internal override bool ConfirmLeave() => !drafted() || NoticeDialog.Choose(Board, "작성 중인 글을 닫으시겠습니까?", "", ["계속 작성", "닫기"], width: 440, height: 146) == 1;
    internal override void FocusFirst() => title.Focus();
}

/// <summary>Figma D8-2 §8-3 글 누름 in the board body.</summary>
internal sealed class PostView : BoardBodyView
{
    private readonly Button list;
    internal PostView(BoardDialog board, BoardClient client, BoardPost post, string workInfo, IReadOnlyList<BoardRow>? rows = null) : base(board)
    {
        var page = new BoardPostPage();
        Children.Add(page);
        list = Action(page.ReturnActions, "목록", () => Board.LeaveBodyAsync(this, false));
        var edit = Action(page.AuthorActions, "수정", () =>
        {
            var editor = new PostEditorView(Board, client, post, workInfo);
            Board.ShowBody(editor, saved =>
            {
                if (saved && editor.SavedPost is { } changed) { post = changed; Render(); }
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });
        var delete = Action(page.AuthorActions, "삭제", async () =>
        {
            if (NoticeDialog.Choose(Board, "이 글을 삭제하시겠습니까?", post.Title, ["삭제", "취소"], width: 440) != 0) return;
            await client.DeleteAsync(post.PostId, Board.Token);
            await Board.LeaveBodyAsync(this, true);
        });
        foreach (var action in new[] { list, edit, delete })
        {
            PublicDialogDesign.StyleAction(action);
            action.Margin = new(action == delete ? 8 : 0, 0, 0, 0);
        }
        delete.Background = delete.BorderBrush = BoardDesign.Brush("#C42B1C");
        delete.Foreground = Brushes.White;
        Render();
        void Render()
        {
            AutomationProperties.SetName(this, post.Title);
            page.Display(post, file => Board.RunBodyAsync(() => DownloadAsync(file)));
            page.AuthorActions.Visibility = post.Mine ? Visibility.Visible : Visibility.Collapsed;
            page.ShowNeighbors(post, rows ?? [], id => Board.RunBodyAsync(async () =>
            {
                post = await client.ReadAsync(id, Board.Token);
                Render();
                Board.ScrollBodyToTop();
                list.Focus();
            }));
        }
    }
    internal override void FocusFirst() => list.Focus();
    private async Task DownloadAsync(BoardAttachment attachment)
    {
        if (!Uri.TryCreate(attachment.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("첨부 주소가 올바르지 않습니다.");
        var picker = new SaveFileDialog { FileName = Path.GetFileName(attachment.FileName) };
        if (picker.ShowDialog(Board) != true) return;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, Board.Token); response.EnsureSuccessStatusCode();
        var temp = picker.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await using var source = await response.Content.ReadAsStreamAsync(Board.Token);
                var buffer = new byte[81920]; int n; long total = 0;
                while ((n = await source.ReadAsync(buffer, Board.Token)) != 0)
                { total += n; if (total > 10 * 1024 * 1024) throw new InvalidDataException("첨부 크기 제한을 초과했습니다."); await target.WriteAsync(buffer.AsMemory(0, n), Board.Token); }
            }
            File.Move(temp, picker.FileName, true); Board.Report("첨부를 저장했습니다.");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
