using System.Windows;
using System.Windows.Controls;

namespace Semojum.Desktop.Dialogs;

public partial class PublicInquiryForm : UserControl
{
    public PublicInquiryForm()
    {
        InitializeComponent();
        PublicDialogDesign.StyleAction(AttachmentButton);
        Consent.Style = PublicDialogDesign.ConsentStyle("Semojum.Feature.Public.Wpf");
        Consent.Margin = new Thickness(0);
        Consent.Padding = new Thickness(0);
        Consent.MinHeight = 20;
        AttachmentButton.Margin = new Thickness(0);
        Rows.SizeChanged += (_, e) => LabelWidth.Width = new GridLength(Math.Min(150, e.NewSize.Width * 0.25));
    }
}
