using System.Windows;
using System.Windows.Controls;

namespace Semojum.Desktop.Dialogs;

public sealed class ImageOrderDialog : FormDialogBase
{
    public string[] Paths { get; private set; }

    public ImageOrderDialog(string[] paths) : base("이미지 올리기", 900, 578, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        Paths = paths.ToArray();
        var browser = new ImageUploadBrowser(paths, Lifetime.Token);
        Fields.Children.Add(browser);
        Fields.Children.Add(new TextBlock { Text = "끌거나 Alt+↑ · ↓ 로 순서를 바꿉니다. 한 장에 두 쪽이 찍힌 그림은 미리 잘라 주십시오.",
            FontSize = 12, Foreground = EditorModalDesign.Color("#5F5F5F"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) });
        var cancel = FooterButton("취소", Close); cancel.BorderBrush = EditorModalDesign.Color("#D1D1D1");
        var upload = FooterButton("올리기", () => { Paths = browser.SelectedPaths; DialogResult = true; }, true);
        upload.IsEnabled = paths.Length != 0;
        browser.InclusionChanged += () => upload.IsEnabled = browser.SelectedPaths.Length != 0;
        browser.Problem += message => Status.Text = message;
    }
}
