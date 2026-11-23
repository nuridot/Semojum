using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

public sealed class NotificationsDialog : CompactDialog
{
    public NotificationsDialog(BoardClient client, string workInfo) : base("알림", 400, 354, popover: true)
    {
        UseLayoutRounding = true;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Board.Wpf;component/Styles/NotificationRow.xaml", UriKind.Relative) });
        var frame = (Border)Content;
        frame.CornerRadius = new(8); frame.Background = Background = BoardDesign.Brush("#FBFBFB"); frame.BorderBrush = BoardDesign.Brush("#EAEAEA");
        var textStyle = new Style(typeof(TextBlock), (Style)FindResource(typeof(TextBlock)));
        textStyle.Setters.Add(new Setter(RenderTransformProperty, new TranslateTransform(0, -1)));
        Resources[typeof(TextBlock)] = textStyle;
        var body = new DockPanel(); Body(body);
        var header = new DockPanel { Height = 48, Margin = new(16, 0, 16, 0) };
        DockPanel.SetDock(header, Dock.Top); body.Children.Add(header);
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "알림", FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var count = new TextBlock { FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var badge = new Border { MinWidth = 20, Height = 20, CornerRadius = new(10), Background = BoardDesign.Brush("#C42B1C"),
            Padding = new(4, 0, 4, 0), Margin = new(8, 0, 0, 0), Child = count, Visibility = Visibility.Collapsed };
        title.Children.Add(badge); header.Children.Add(title);
        var all = TextAction("공지 전체 보기", () =>
        {
            var dialog = BoardDialog.Notices(client, workInfo); dialog.Owner = this; dialog.ShowDialog();
            return Task.CompletedTask;
        });
        var footer = new Border { Height = 44, BorderThickness = new(0, 1, 0, 0), BorderBrush = BoardDesign.Brush("#EAEAEA"), Child = all };
        DockPanel.SetDock(footer, Dock.Bottom); body.Children.Add(footer);
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        StripListPadding(list);
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FocusableProperty, false));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }));
        list.ItemContainerStyle = style;
        var empty = new TextBlock { Text = "새 알림이 없습니다.", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        var row = new FrameworkElementFactory(typeof(NotificationView));
        row.SetValue(NotificationView.OpenActionProperty, (Action<BoardAlert>)(entry => { _ = RunAsync(() => OpenAsync(entry)); }));
        list.ItemTemplate = new DataTemplate { VisualTree = row };
        AutomationProperties.SetName(list, "알림 목록");
        var content = new Grid(); content.Children.Add(list); content.Children.Add(empty); body.Children.Add(content);
        var loading = false;
        Closing += (_, e) => { if (loading) { e.Cancel = false; Lifetime.Cancel(); } };
        async Task LoadAsync()
        {
            loading = true;
            try
            {
                var result = await client.NotificationsAsync(Lifetime.Token);
                if (Lifetime.IsCancellationRequested) return;
                var entries = result.Entries.ToArray();
                list.ItemsSource = entries; list.ToolTip = $"안 읽음 {result.UnreadCount}";
                count.Text = result.UnreadCount.ToString(CultureInfo.CurrentCulture);
                AutomationProperties.SetName(badge, list.ToolTip.ToString());
                badge.Visibility = result.UnreadCount == 0 ? Visibility.Collapsed : Visibility.Visible;
                empty.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally { loading = false; }
        }
        async Task OpenAsync(BoardAlert entry)
        {
            if (!list.Items.Contains(entry)) return;
            if (entry is BoardNoticeAlert notice)
            {
                var details = await client.NoticeAsync(notice.NoticeId, Lifetime.Token);
                BoardNoticeDialog.ShowFor(this, details);
            }
            else if (entry is BoardNotification answer)
            {
                var post = await client.ReadAsync(answer.PostId, Lifetime.Token);
                var dialog = BoardDialog.Reading(client, post, workInfo); dialog.Owner = this; dialog.ShowDialog();
            }
            await LoadAsync();
        }
        Task ReadAllAsync() => RunAsync(async () => { await client.ReadAllAsync(Lifetime.Token); await LoadAsync(); });
        var readAll = TextAction("모두 읽음", ReadAllAsync);
        DockPanel.SetDock(readAll, Dock.Right); header.Children.Insert(0, readAll);
        OnLoad(LoadAsync);
    }

    private static Button TextAction(string label, Func<Task> run)
    {
        var button = Button(label, run);
        button.Padding = button.Margin = new(0); button.MinWidth = button.MinHeight = 0;
        button.Background = Brushes.Transparent; button.BorderThickness = new(0); button.Foreground = BoardDesign.Brush("#003E92");
        button.Template = EditorModalDesign.ButtonTemplate(typeof(Button)); return button;
    }
}

public sealed class NotificationView : ContentControl
{
    public static readonly DependencyProperty OpenActionProperty = DependencyProperty.Register(nameof(OpenAction), typeof(Action<BoardAlert>), typeof(NotificationView));
    public Action<BoardAlert>? OpenAction { get => (Action<BoardAlert>?)GetValue(OpenActionProperty); set => SetValue(OpenActionProperty, value); }

    public NotificationView() => DataContextChanged += (_, _) =>
    {
        if (DataContext is not BoardAlert item) { Content = null; return; }
        var row = new DockPanel { Margin = new(16, 12, 16, 12) };
        row.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new(4), VerticalAlignment = VerticalAlignment.Top,
            Margin = new(0, 0, 12, 0), Background = item.Read ? Brushes.Transparent : BoardDesign.Brush("#005FB8") });
        var text = new StackPanel(); row.Children.Add(text);
        var heading = new DockPanel { Height = 20 };
        if (item is BoardNoticeAlert { Important: true }) heading.Children.Add(BoardNoticeDialog.ImportanceBadge());
        heading.Children.Add(new TextBlock { Text = item.Title, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = item.Read ? FontWeights.Normal : FontWeights.SemiBold, Foreground = BoardDesign.Brush(item.Read ? "#5F5F5F" : "#1A1A1A") });
        text.Children.Add(heading);
        var stamp = item.LocalTime is { } date ? date.ToString(date.Date == DateTime.Today ? "'오늘' HH:mm" : "MM.dd", CultureInfo.InvariantCulture) : item.Timestamp;
        text.Children.Add(new TextBlock { Text = stamp, FontSize = 12, Height = 16, Margin = new(0, 4, 0, 0), Foreground = BoardDesign.Brush("#8A8A8A") });
        var surface = new Border { Child = row, BorderThickness = new(0, 1, 0, 0), BorderBrush = BoardDesign.Brush("#EAEAEA") };
        var button = new Button { Content = surface, Height = 65, MinHeight = 0, MinWidth = 0, Padding = new(0), Margin = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
            Background = BoardDesign.Brush(item.Read ? "#FBFBFB" : "#EFF6FC"), BorderThickness = new(0), ToolTip = item is BoardNotification answer ? answer.Display : item.Title,
        };
        button.SetResourceReference(TemplateProperty, "NotificationButton");
        AutomationProperties.SetName(button, item.Title);
        AutomationProperties.SetHelpText(button, (item.Read ? "읽음" : "안 읽음") + " · " + stamp);
        button.Click += (_, _) => OpenAction?.Invoke(item);
        Content = button;
    };
}
