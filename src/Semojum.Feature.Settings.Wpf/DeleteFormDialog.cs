using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

public sealed class DeleteFormDialog : ConversionFormDialog
{
    public DeleteFormDialog(string name) : base("사용자 양식 지우기", 440, 170)
    {
        Fields.Children.Add(new TextBlock
        {
            Text = $"「{name}」 양식을 지웁니다. 이 양식으로 이미 변환한 문서는 그대로 남습니다.",
            FontSize = 14, LineHeight = 20, LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap
        });
        CancelButton();
        var delete = FooterButton("지우기", () => { DialogResult = true; }, true);
        delete.Background = delete.BorderBrush = EditorModalDesign.Color("#C42B1C");
        delete.Foreground = Brushes.White;
    }
}
