using System.Windows;
using System.Windows.Controls;
using Semojum.Feature.Board;

namespace Semojum.Desktop.Dialogs;

internal partial class BoardWriteForm : UserControl
{
    internal BoardWriteForm(string heading, BoardPost? post)
    {
        InitializeComponent();
        Heading.Text = heading;
        TitleInput.Text = post?.Title ?? "";
        ContentInput.Text = post?.Content ?? "";
        Secret.Style = IncludeWork.Style = BoardDesign.CheckStyle();
        Secret.IsChecked = post?.Secret == true;
        IncludeWork.IsChecked = post is null;
        var code = post is null ? BoardCategories.Default : post.Category;
        foreach (var (value, label) in BoardCategories.All)
            Categories.Children.Add(new RadioButton { Tag = value, Content = label, IsChecked = code == value,
                Margin = new Thickness(0, 0, 16, 0), FontSize = 14 });
        PublicDialogDesign.StyleAction(ChooseFile);
        ChooseFile.Width = 88; ChooseFile.Margin = new(0);
    }

    internal string? SelectedCategory => Categories.Children.OfType<RadioButton>()
        .FirstOrDefault(radio => radio.IsChecked == true)?.Tag as string;
}
