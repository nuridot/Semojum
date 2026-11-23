using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Automation;
using Semojum.Core;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;
public sealed class MemoThreadDialog : MemoPopover
{
    internal ListBox MemoItems { get; } = new() { BorderThickness = new(0), Background = Brushes.Transparent, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    internal CheckBox ShowResolved { get; } = new() { Content = "해결한 메모 포함" };
    internal Button ResolveButton { get; }
    internal Button ReplyButton { get; }
    internal ContextMenu CommandMenu { get; } = new();
    internal TextBox ReplyInput { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 14, MinHeight = 32, MaxHeight = 160,
        MaxLength = 2000, Padding = new(11, 5, 11, 5), VerticalContentAlignment = VerticalAlignment.Center, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public NavigationLocation? Destination { get; private set; }
    public string? DestinationMemoId { get; private set; }
    internal void ApplyReplyAppearance(bool highContrast) => MemoTextInputAppearance.Apply(ReplyInput, highContrast);
    public MemoThreadDialog(EditorBlock block, string panel, int line, string lineText, Action<Action> edit,
        DocumentSession? model = null, bool showResolved = false, string? author = null, string? selectedMemoId = null) : base(360, 178)
    {
        MinHeight = 58;
        var body = new DockPanel { Margin = new(0, 8, 0, 8) };
        Body(body);
        var list = MemoItems;
        var input = ReplyInput;
        MemoTextInputAppearance.Attach(this, input);
        var replyHost = new DockPanel { Margin = new(12, 8, 12, 0) };
        DockPanel.SetDock(replyHost, Dock.Bottom); body.Children.Add(replyHost);
        AutomationProperties.SetName(input, "메모 회신");
        input.ToolTip = "Enter: 회신 추가 · Shift+Enter: 줄 바꿈";
        var placeholder = new TextBlock { Text = "회신...", FontSize = 14, Foreground = EditorModalDesign.Color("#8A8A8A"),
            Margin = new(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        input.TextChanged += (_, _) => placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var inputHost = new Grid(); inputHost.Children.Add(input); inputHost.Children.Add(placeholder);
        StripListPadding(list);
        ScrollViewer.SetCanContentScroll(list, false);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        void FitRows() => list.MaxHeight = Math.Max(60, Math.Min(MaxHeight, SystemParameters.WorkArea.Height) - input.ActualHeight - 48);
        Loaded += (_, _) => FitRows(); input.SizeChanged += (_, _) => FitRows();
        body.Children.Add(list);

        (LineMemo Memo, int Index)? selectedReply = null;
        var conversation = new FrameworkElementFactory(typeof(MemoConversationView));
        conversation.SetValue(MemoConversationView.DeleteActionProperty, (Action<LineMemo, int?>)Delete);
        conversation.SetValue(MemoConversationView.ResolveActionProperty, (Action<LineMemo>)Resolve);
        conversation.SetValue(MemoConversationView.SelectActionProperty, (Action<LineMemo, int?>)((memo, reply) =>
        {
            list.SelectedItem = memo;
            selectedReply = reply is { } index ? (memo, index) : null;
        }));
        if (model is not null) conversation.SetBinding(MemoConversationView.CanEditProperty, new Binding(nameof(DocumentSession.IsReadOnly)) { Source = model, Converter = InverseReadOnly.Instance });
        list.ItemTemplate = new DataTemplate { VisualTree = conversation };
        var container = new Style(typeof(ListBoxItem));
        container.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        container.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        container.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }));
        list.ItemContainerStyle = container;
        bool Writable() => model is null || !model.IsReadOnly && model.Blocks.Contains(block);
        int CurrentIndex(LineMemo memo)
        {
            if (!Writable()) return -1;
            var index = block.Memos.IndexOf(memo);
            if (index >= 0 && ReferenceEquals(block.Memos[index], memo)) return index;
            Status.Text = "메모가 변경되었습니다. 목록을 다시 확인해 주세요."; Refresh(); return -1;
        }
        void Refresh()
        {
            var selected = (list.SelectedItem as LineMemo)?.LocalId ?? selectedMemoId;
            list.ItemsSource = block.Memos.Where(m => m.Panel == panel && m.LineIndex == line && (ShowResolved.IsChecked == true || !m.Resolved)).OrderBy(m => m.CreatedAt).ToArray();
            list.SelectedItem = list.Items.Cast<LineMemo>().FirstOrDefault(m => m.LocalId == selected) ?? list.Items.Cast<LineMemo>().FirstOrDefault();
            list.ToolTip = lineText;
        }
        void Delete(LineMemo shown, int? reply)
        {
            var index = CurrentIndex(shown);
            if (model is null || index < 0) return;
            if (reply is { } i)
            {
                if (shown.Replies is null || i < 0 || i >= shown.Replies.Length) return;
                edit(() => block.Memos[index] = shown with { Replies = shown.Replies.Where((_, n) => n != i).ToArray() });
            }
            else edit(() => block.Memos.RemoveAt(index));
            Refresh();
        }
        void Resolve(LineMemo shown)
        {
            var index = CurrentIndex(shown);
            if (model is null || index < 0) return;
            edit(() => block.Memos[index] = shown with { Resolved = !shown.Resolved, ResolvedAt = shown.Resolved ? null : DateTimeOffset.Now });
            if (!shown.Resolved) Close(); else Refresh();
        }
        void Move(int delta)
        {
            if (model is null) return;
            var anchors = model.Blocks.SelectMany(b => b.Memos.Where(m => ShowResolved.IsChecked == true || !m.Resolved)
                .Select(m => (Block: b, m.Panel, m.LineIndex))).Distinct()
                .OrderBy(a => a.Block.Page).ThenBy(a => model.Blocks.IndexOf(a.Block)).ThenBy(a => a.Panel).ThenBy(a => a.LineIndex).ToList();
            var index = anchors.FindIndex(a => a.Block == block && a.Panel == panel && a.LineIndex == line);
            var next = index < 0 ? (delta > 0 ? 0 : anchors.Count - 1) : index + delta;
            if (next < 0 || next >= anchors.Count) { Status.Text = delta > 0 ? "마지막 메모입니다." : "첫 메모입니다."; return; }
            var a = anchors[next]; var targetPanel = a.Panel == "TEXT" ? SearchPanel.Text : SearchPanel.Braille;
            var offsets = DocumentNavigation.LineOffsets(DocumentSearch.Content(a.Block, targetPanel));
            if (a.LineIndex < 0 || a.LineIndex >= offsets.Length) { Status.Text = "원래 줄이 삭제된 메모입니다. 메모 목록에서 확인해 주세요."; return; }
            Destination = new(a.Block, targetPanel, offsets[a.LineIndex]); Close();
        }
        void Overview()
        {
            if (model is null) return;
            var overview = new MemoListDialog(model, (list.SelectedItem as LineMemo)?.LocalId, ShowResolved.IsChecked == true) { Owner = this };
            if (overview.ShowDialog() != true) return;
            ShowResolved.IsChecked = overview.IncludeResolved;
            Destination = overview.Destination; DestinationMemoId = overview.DestinationMemoId; Close();
        }
        void NewMemo()
        {
            if (!Writable()) return;
            var dialog = new MemoInputDialog(author) { Owner = this };
            if (dialog.ShowDialog() != true || !Writable()) return;
            var memo = new LineMemo(Guid.NewGuid().ToString("N"), null, panel, line, lineText[..Math.Min(80, lineText.Length)], dialog.Value, DateTimeOffset.Now, Author: author);
            edit(() => block.Memos.Add(memo)); Refresh(); list.SelectedItem = memo;
        }
        void EditSelected()
        {
            if (!Writable() || list.SelectedItem is not LineMemo memo) return;
            int? reply = selectedReply is { } target && ReferenceEquals(target.Memo, memo) ? target.Index : null;
            var message = reply is { } r ? memo.Replies![r] : null;
            var dialog = new MemoInputDialog(message is null ? memo.Author : message.Author) { Owner = this };
            dialog.Input.Text = message?.Body ?? memo.Body;
            if (dialog.ShowDialog() != true) return;
            var index = CurrentIndex(memo);
            if (index < 0) return;
            if (reply is { } i)
            {
                var replies = memo.Replies!.ToArray(); replies[i] = replies[i] with { Body = dialog.Value };
                edit(() => block.Memos[index] = memo with { Replies = replies });
            }
            else edit(() => block.Memos[index] = memo with { Body = dialog.Value });
            Refresh();
        }
        ResolveButton = Action("해결·다시 열기", () => { if (list.SelectedItem is LineMemo memo) Resolve(memo); });
        if (model is null) ResolveButton.IsEnabled = false;
        else ResolveButton.SetBinding(IsEnabledProperty, new Binding(nameof(DocumentSession.IsReadOnly)) { Source = model, Converter = InverseReadOnly.Instance });
        ShowResolved.IsChecked = showResolved;
        ShowResolved.Checked += (_, _) => Refresh(); ShowResolved.Unchecked += (_, _) => Refresh();
        ReplyButton = Action("회신 추가", () =>
        {
            if (!Writable()) return;
            if (string.IsNullOrWhiteSpace(input.Text)) throw new ArgumentException("회신을 입력해 주세요.");
            if (list.SelectedItem is not LineMemo memo) throw new ArgumentException("회신할 메모를 선택하거나 새 메모를 추가해 주세요.");
            var index = CurrentIndex(memo);
            if (index < 0) return;
            edit(() => block.Memos[index] = memo with { Replies = [.. memo.Replies ?? [], new(author, input.Text.Trim(), DateTimeOffset.Now)] });
            input.Clear(); Status.Text = ""; Refresh();
        });
        Actions.Children.Remove(ReplyButton); ReplyButton.IsDefault = false;
        ReplyButton.Width = ReplyButton.Height = 32; ReplyButton.MinWidth = ReplyButton.MinHeight = 0;
        ReplyButton.VerticalAlignment = VerticalAlignment.Bottom;
        ReplyButton.Margin = new(8, 0, 0, 0); ReplyButton.Padding = new(0); ReplyButton.ToolTip = "회신 추가";
        ReplyButton.Template = EditorModalDesign.ButtonTemplate(typeof(Button));
        ReplyButton.Background = ReplyButton.BorderBrush = EditorModalDesign.Color("#005FB8");
        ReplyButton.Content = EditorModalDesign.Icon("memo-send", assemblyName: "Semojum.Feature.Editor.Wpf");
        AutomationProperties.SetName(ReplyButton, "회신 추가");
        DockPanel.SetDock(ReplyButton, Dock.Right); replyHost.Children.Add(ReplyButton); replyHost.Children.Add(inputHost);
        if (model is not null) replyHost.SetBinding(IsEnabledProperty, new Binding(nameof(DocumentSession.IsReadOnly)) { Source = model, Converter = InverseReadOnly.Instance });
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            { ReplyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); e.Handled = true; }
        };
        var menu = CommandMenu;
        menu.Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Editor.Wpf;component/Styles/MemoViews.xaml", UriKind.Relative) });
        menu.Style = (Style)menu.FindResource("MemoMenu");
        body.ContextMenu = menu; list.ContextMenu = menu;
        MenuItem Add(string label, string icon, Action run, string assembly = "Semojum.Feature.Editor.Wpf")
        {
            var item = new MenuItem { Header = label, Icon = EditorModalDesign.Icon(icon, assemblyName: assembly), Style = (Style)menu.FindResource("MemoMenuItem") };
            item.Click += (_, _) => { try { run(); } catch (Exception ex) { Status.Text = ex.Message; } };
            menu.Items.Add(item); return item;
        }
        void Divider() => menu.Items.Add(new Separator { Style = (Style)menu.FindResource("MemoMenuSeparator") });
        var resolve = Add("해결", "memo-menu-resolve", () => { if (list.SelectedItem is LineMemo memo) Resolve(memo); });
        Add("이전 메모", "ribbon-chevl", () => Move(-1), "Semojum.Mockup");
        Add("다음 메모", "ribbon-chevr", () => Move(1), "Semojum.Mockup");
        Add("메모 목록", "memo-menu-memos", Overview).IsEnabled = model is not null;
        Divider();
        var replyItem = Add("회신 추가", "memo-menu-reply", () =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) input.Focus(); else ReplyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        });
        var newItem = Add("새 메모", "memo-menu-memoadd", NewMemo);
        var editItem = Add("선택 수정", "memo-menu-edit", EditSelected);
        Divider(); Add("닫기", "line-preview-close", Close);
        menu.Opened += (_, _) =>
        {
            var selected = list.SelectedItem as LineMemo;
            resolve.Header = selected?.Resolved == true ? "해결 되돌리기" : "해결";
            resolve.IsEnabled = model is not null && Writable() && selected is not null;
            newItem.IsEnabled = Writable();
            editItem.IsEnabled = replyItem.IsEnabled = Writable() && selected is not null;
        };
        Refresh();
    }
}

/// <summary>
/// One hover bubble shared by every memo marker (text editors and the braille grid): hovering a marker opens its thread
/// non-modally beside the pointer without taking focus, and it closes 0.3s after the pointer has left both the marker and the bubble.
/// </summary>
public sealed class MemoHoverBubble(Func<EditorBlock, string, int, MemoThreadDialog?> open)
{
    internal static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(300);
    private System.Windows.Threading.DispatcherTimer? closeTimer;
    private (EditorBlock Block, string Panel, int Line)? anchor;
    private bool overMarker, overBubble;
    public MemoThreadDialog? Current { get; private set; }
    /// <summary>The pointer is over the memo marker of <paramref name="line"/>; false when no bubble can be shown (for example in a read-only document).</summary>
    public bool Enter(UIElement marker, EditorBlock block, string panel, int line)
    {
        if (Current is { IsVisible: true } && anchor == (block, panel, line)) { overMarker = true; closeTimer?.Stop(); return true; }
        Close();
        if (open(block, panel, line) is not { } bubble) return false;
        bubble.ShowActivated = false; bubble.WindowStartupLocation = WindowStartupLocation.Manual;
        UIElement? reference = PresentationSource.FromVisual(marker) is not null ? marker : bubble.Owner;
        if (reference is not null && PresentationSource.FromVisual(reference)?.CompositionTarget is { } target)
        {
            var pointer = target.TransformFromDevice.Transform(reference.PointToScreen(System.Windows.Input.Mouse.GetPosition(reference)));
            // Beside the pointer; on its left when the screen has no room on the right, so the bubble never covers the pointer.
            bubble.Left = pointer.X + 12 + bubble.Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ? pointer.X - 12 - bubble.Width : pointer.X + 12;
            bubble.Top = pointer.Y - 8;
        }
        bubble.MouseEnter += (_, _) => { if (Current == bubble) { overBubble = true; closeTimer?.Stop(); } };
        bubble.MouseLeave += (_, _) => { if (Current == bubble) { overBubble = false; ScheduleClose(); } };
        bubble.Deactivated += (_, _) => { if (Current == bubble) ScheduleClose(); };
        bubble.CommandMenu.Closed += (_, _) => { if (Current == bubble) ScheduleClose(); };
        bubble.Closed += (_, _) => { if (Current == bubble) { Current = null; anchor = null; overMarker = overBubble = false; closeTimer?.Stop(); } };
        Current = bubble; anchor = (block, panel, line); overMarker = true; overBubble = false;
        bubble.Show(); return true;
    }
    /// <summary>The pointer left the memo marker.</summary>
    public void Leave() { overMarker = false; ScheduleClose(); }
    public void Close() { closeTimer?.Stop(); Current?.Close(); }
    private void ScheduleClose()
    {
        if (Current is null) return;
        if (closeTimer is null)
        {
            closeTimer = new() { Interval = CloseDelay };
            closeTimer.Tick += (_, _) =>
            {
                closeTimer.Stop();
                // Keep a bubble the user is typing in, or one that owns an open memo input.
                if (Current is { } bubble && !overMarker && !overBubble && !bubble.IsActive && !bubble.CommandMenu.IsOpen && !bubble.OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) bubble.Close();
            };
        }
        closeTimer.Stop(); closeTimer.Start();
    }
}

internal sealed class InverseReadOnly : IValueConverter
{
    internal static readonly InverseReadOnly Instance = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value is bool readOnly && !readOnly;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
}

public sealed class MemoConversationView : ContentControl
{
    public static readonly DependencyProperty DeleteActionProperty = DependencyProperty.Register(nameof(DeleteAction), typeof(Action<LineMemo, int?>), typeof(MemoConversationView));
    public Action<LineMemo, int?>? DeleteAction { get => (Action<LineMemo, int?>?)GetValue(DeleteActionProperty); set => SetValue(DeleteActionProperty, value); }
    public static readonly DependencyProperty SelectActionProperty = DependencyProperty.Register(nameof(SelectAction), typeof(Action<LineMemo, int?>), typeof(MemoConversationView));
    public Action<LineMemo, int?>? SelectAction { get => (Action<LineMemo, int?>?)GetValue(SelectActionProperty); set => SetValue(SelectActionProperty, value); }
    public static readonly DependencyProperty ResolveActionProperty = DependencyProperty.Register(nameof(ResolveAction), typeof(Action<LineMemo>), typeof(MemoConversationView));
    public Action<LineMemo>? ResolveAction { get => (Action<LineMemo>?)GetValue(ResolveActionProperty); set => SetValue(ResolveActionProperty, value); }
    public static readonly DependencyProperty CanEditProperty = DependencyProperty.Register(nameof(CanEdit), typeof(bool), typeof(MemoConversationView), new PropertyMetadata(false, (d, _) => ((MemoConversationView)d).Refresh()));
    public bool CanEdit { get => (bool)GetValue(CanEditProperty); set => SetValue(CanEditProperty, value); }
    public MemoConversationView() => DataContextChanged += (_, _) => Refresh();
    private void Refresh()
    {
        if (DataContext is not LineMemo memo) { Content = null; return; }
        var messages = new StackPanel();
        void Add(string? author, string text, DateTimeOffset date, int? reply)
        {
            var row = new StackPanel { Margin = new(12, 8, 12, 8), Background = Brushes.Transparent };
            row.PreviewMouseDown += (_, _) => SelectAction?.Invoke(memo, reply);
            row.GotKeyboardFocus += (_, _) => SelectAction?.Invoke(memo, reply);
            var heading = new DockPanel { Height = 24 };
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            void Command(string label, string icon, Action run)
            {
                var button = new Button { Content = EditorModalDesign.Icon(icon, assemblyName: "Semojum.Feature.Editor.Wpf"), ToolTip = label,
                    Width = 24, Height = 24, MinHeight = 0, MinWidth = 0, Padding = new(0), Margin = new(actions.Children.Count == 0 ? 0 : 4, 0, 0, 0),
                    IsEnabled = CanEdit, Background = Brushes.Transparent, BorderThickness = new(0), Template = EditorModalDesign.ButtonTemplate(typeof(Button)) };
                AutomationProperties.SetName(button, label);
                button.Click += (_, _) => run();
                actions.Children.Add(button);
            }
            if (reply is null) Command(memo.Resolved ? "해결 되돌리기" : "메모 해결", "memo-row-resolve", () => ResolveAction?.Invoke(memo));
            Command(reply is null ? "메모 삭제" : "답글 삭제", "table-trash", () => DeleteAction?.Invoke(memo, reply));
            DockPanel.SetDock(actions, Dock.Right); heading.Children.Add(actions);
            var age = DateTimeOffset.Now - date;
            var when = age < TimeSpan.FromMinutes(1) ? "방금" : age < TimeSpan.FromHours(1) ? $"{(int)age.TotalMinutes}분 전"
                : age < TimeSpan.FromDays(1) ? $"{(int)age.TotalHours}시간 전" : age < TimeSpan.FromDays(7) ? $"{(int)age.TotalDays}일 전" : date.ToLocalTime().ToString("MM-dd");
            var identity = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = string.IsNullOrWhiteSpace(author) ? "작성자 미상" : author, FontSize = 14,
                MaxWidth = 165, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = author };
            identity.Children.Add(name);
            identity.Children.Add(new TextBlock { Text = when, FontSize = 12, Foreground = EditorModalDesign.Color("#8A8A8A"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 0, 0), ToolTip = date.ToLocalTime().ToString("yyyy-MM-dd HH:mm") });
            heading.Children.Add(identity); row.Children.Add(heading);
            row.Children.Add(new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 16,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new(0, 4, 0, 0) });
            messages.Children.Add(row);
        }
        Add(memo.Author, memo.Body, memo.CreatedAt, null);
        var replies = memo.Replies ?? [];
        for (var i = 0; i < replies.Length; i++) Add(replies[i].Author, replies[i].Body, replies[i].CreatedAt, i);
        Content = messages;
    }
}
