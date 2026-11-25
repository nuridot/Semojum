using System.Windows;
using System.Windows.Controls;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

internal sealed class SocialSignupDialog : FormDialogBase
{
    private readonly SignupAgreements agreements = new();
    internal string? MarketingVersion => agreements.MarketingVersion;

    internal SocialSignupDialog(SocialSignupTicket ticket) : base("소셜 회원가입", 420, 390)
    {
        Fields.Children.Add(new TextBlock { Text = "누리닷에 가입하려면 필수 약관에 동의해 주세요.", TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(ticket.Nickname)) Profile("별명", ticket.Nickname);
        if (!string.IsNullOrWhiteSpace(ticket.Email)) Profile("이메일", ticket.Email);
        Fields.Children.Add(agreements);
        CancelButton();
        FooterButton("가입하기", () =>
        {
            if (agreements.RequiredAccepted) DialogResult = true;
            else Status.Text = "필수 약관 두 항목에 동의해 주세요.";
        }, true);
    }

    private void Profile(string label, string value) => Fields.Children.Add(new TextBlock
        { Text = label + "  " + value, Margin = new(0,8,0,0), TextWrapping = TextWrapping.Wrap });
}
