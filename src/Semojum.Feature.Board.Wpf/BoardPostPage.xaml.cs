using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

internal partial class BoardPostPage : UserControl
{
    internal BoardPostPage() => InitializeComponent();

    internal void Display(BoardPost post, Func<BoardAttachment, Task> download)
    {
        Heading.Text = (post.Secret ? "비밀 · " : "") + (BoardCategories.Label(post.Category) is { } label ? $"[{label}] " : "") + post.Title;
        Metadata.Text = $"{post.Author} · {post.CreatedAt} · 조회 {post.ViewCount}" + (post.Secret ? " · 글쓴이와 운영자만 열람" : "");
        Message.Text = post.Content;
        Files.Children.Clear();
        foreach (var file in post.Attachments)
        {
            var button = CompactDialog.Button("", () => download(file));
            button.Template = (ControlTemplate)FindResource("RowAction");
            button.Width = double.NaN; button.Height = 40; button.Margin = new(0, 16, 0, 0);
            button.Background = BoardDesign.Brush("#F9F9F9"); button.BorderThickness = new(0);
            button.Padding = new(16,0,16,0); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); row.ColumnDefinitions.Add(new());
            var icon = BoardDesign.Icon("attach"); icon.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(icon);
            var size = file.Size >= 1024 * 1024 ? $"{file.Size / (1024d * 1024):0.#}MB" : $"{file.Size / 1024d:0.#}KB";
            var name = new TextBlock { Text = $"{file.FileName} ({size})", FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1); row.Children.Add(name); button.Content = row;
            name.Foreground = BoardDesign.Brush("#003E92"); button.ToolTip = file.FileName;
            AutomationProperties.SetName(button, file.FileName + " 다운로드");
            Files.Children.Add(button);
        }
        Answers.Children.Clear();
        foreach (var answer in post.Answers)
        {
            var reply = new StackPanel();
            reply.Children.Add(new TextBlock { Text = $"{(answer.FromAuthor ? "작성자 추가 문의" : "운영자 답변")} · {answer.AnsweredAt}",
                FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = BoardDesign.Brush("#003E92"), TextWrapping = TextWrapping.Wrap });
            reply.Children.Add(new TextBlock { Text = answer.Content, Margin = new(0,8,0,0), FontSize = 14, TextWrapping = TextWrapping.Wrap });
            Answers.Children.Add(new Border { Child = reply, CornerRadius = new(8), Background = BoardDesign.Brush("#EFF6FC"), Padding = new(20,16,20,16), Margin = new(0,16,0,0) });
        }
    }

    internal void ShowNeighbors(BoardPost post, IReadOnlyList<BoardRow> rows, Func<long, Task> open)
    {
        Neighbors.Children.Clear();
        Neighbors.Margin = new(0);
        if (!rows.Any(r => r.NoticeId is null && r.PostId == post.PostId)) return;
        var posts = rows.Where(r => r.NoticeId is null);
        Add("이전 글", posts.Where(r => r.PostId < post.PostId).MaxBy(r => r.PostId), true);
        Add("다음 글", posts.Where(r => r.PostId > post.PostId).MinBy(r => r.PostId), false);
        void Add(string label, BoardRow? row, bool previous)
        {
            if (row is null) return;
            var button = CompactDialog.Button("", () => open(row.PostId));
            button.Template = (ControlTemplate)FindResource("RowAction");
            button.Width = double.NaN; button.Height = 44; button.Margin = new(0);
            button.Background = Brushes.White; button.BorderBrush = BoardDesign.Brush("#EAEAEA"); button.BorderThickness = new(0,0,0,1);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new(12,0,12,0);
            var content = new DockPanel();
            var arrow = BoardDesign.Icon("chevron-down"); arrow.Margin = new(0,0,12,0);
            if (previous) arrow.LayoutTransform = new RotateTransform(180);
            content.Children.Add(arrow);
            content.Children.Add(new TextBlock { Text = label, Width = 68, FontSize = 12, Foreground = BoardDesign.Brush("#5F5F5F") });
            var title = $"{row.Number}. {(row.Secret ? "비밀 · " : "")}{row.Title}";
            content.Children.Add(new TextBlock { Text = title, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis });
            button.Content = content; AutomationProperties.SetName(button, $"{label} {title}");
            Neighbors.Children.Add(button);
        }
        if (Neighbors.Children.Count > 0) Neighbors.Margin = new(0,17,0,0);
    }
}
