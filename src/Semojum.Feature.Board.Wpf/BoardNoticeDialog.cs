using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

public sealed class BoardNoticeDialog : FormDialogBase
{
    private static bool Important(JsonElement notice) => notice.TryGetProperty("important", out var value) && value.ValueKind == JsonValueKind.True;

    public static void ShowFor(Window owner, JsonElement notice)
    {
        if (Important(notice)) new BoardNoticeDialog(notice) { Owner = owner }.ShowDialog();
        else NoticeDialog.Choose(owner, notice.GetProperty("title").GetString() ?? "공지", notice.GetProperty("body").GetString() ?? "", ["확인"], height: 316);
    }

    private BoardNoticeDialog(JsonElement notice) : base(Important(notice) ? "중요 공지" : "공지", 520, 238,
        EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        var heading = new DockPanel();
        if (Important(notice)) heading.Children.Add(ImportanceBadge());
        var title = notice.GetProperty("title").GetString() ?? "";
        heading.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold,
            LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextWrapping = TextWrapping.Wrap });
        Fields.Children.Add(heading);
        if (notice.TryGetProperty("createdAt", out var value) && value.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
            Fields.Children.Add(new TextBlock { Text = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), FontSize = 12,
                Foreground = BoardDesign.Brush("#8A8A8A"), Height = 16, Margin = new(0, 16, 0, 0) });
        Fields.Children.Add(new TextBlock { Text = notice.GetProperty("body").GetString(), TextWrapping = TextWrapping.Wrap,
            FontSize = 14, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Margin = new(0, 16, 0, 0) });
        FooterButton("확인", Close, true);
    }

    internal static Border ImportanceBadge() => new() { Height = 20, CornerRadius = new(4), Padding = new(8, 0, 8, 0),
        Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top, Background = BoardDesign.Brush("#FDE7E9"),
        Child = new TextBlock { Text = "중요", FontSize = 12, Foreground = BoardDesign.Brush("#C42B1C"), VerticalAlignment = VerticalAlignment.Center } };
}
