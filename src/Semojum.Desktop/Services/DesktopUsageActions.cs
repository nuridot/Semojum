using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using Semojum.Feature.Board;
using Semojum.Feature.Usage.Wpf;

namespace Semojum.Desktop.Services;

/// <summary>Connects usage-screen navigation to the desktop's actual inquiry and board windows.</summary>
internal static class DesktopUsageActions
{
    internal static UsageActions Create(SemojumApiClient api, string workInfo)
    {
        var board = new BoardClient(api);
        var session = api.SessionMarker;
        return new(
            owner => { new PublicInquiryDialog(api, standalone: true) { Owner = owner }.ShowDialog(); },
            owner =>
            {
                if (session is null || session != api.SessionMarker) throw new InvalidOperationException("로그인한 뒤 마이페이지를 다시 열어 주세요.");
                var page = owner as MyPageWindow ?? owner.Owner as MyPageWindow;
                return new PublicInquiryDialog(api, standalone: true, initialEmail: page?.ReplyEmail,
                    sendMemberInquiry: async (message, token) =>
                    {
                        // Even a secret board post exposes its list title. Keep all free-text fields in the private body.
                        var lines = new List<string>();
                        if (message.Title.Length > 0) lines.Add("제목: " + message.Title);
                        lines.Add("답 받을 주소: " + message.Email);
                        if (message.Organization.Length > 0) lines.Add("기관명: " + message.Organization);
                        lines.Add("");
                        lines.Add(message.Body);
                        await board.SaveAsync(null, BoardCategories.Label(message.Category) + " 문의", string.Join("\n", lines),
                            true, [], message.Files, message.Category, token, session);
                    }) { Owner = owner }.ShowDialog() == true;
            },
            (owner, post) => { var reading = BoardDialog.Reading(board, post, workInfo, initiallyMine: true); reading.Owner = owner; reading.ShowDialog(); },
            owner => { new BoardDialog(board, workInfo, initiallyMine: true) { Owner = owner }.ShowDialog(); },
            OpenNotifications: owner => { new NotificationsDialog(board, workInfo) { Owner = owner }.ShowDialog(); });
    }
}
