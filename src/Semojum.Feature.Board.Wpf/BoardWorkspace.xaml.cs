using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Semojum.Desktop.Dialogs;

public interface IBoardWorkspaceHost
{
    void ReturnToEditor(BoardDialog board);
    void OpenBoardAccountMenu(BoardDialog board, Button anchor);
}

internal partial class BoardWorkspace : UserControl
{
    private readonly BoardClient client;
    private readonly BoardDialog board;
    internal BoardWorkspace(BoardDialog board, BoardClient client, string workInfo)
    {
        InitializeComponent();
        this.client = client;
        this.board = board;
        LoginName.Text = client.CurrentLoginId ?? "";
        Back.Click += (_, _) => { if (Host() is { } host) host.ReturnToEditor(board); else board.Close(); };
        Account.Click += (_, _) =>
        {
            if (Host() is { } host) host.OpenBoardAccountMenu(board, Account);
            else { var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = LoginName.Text, IsEnabled = false }); menu.PlacementTarget = Account; menu.IsOpen = true; }
        };
        Notifications.Click += async (_, _) =>
        {
            new NotificationsDialog(client, workInfo) { Owner = board }.ShowDialog();
            await RefreshBadgeAsync();
        };
        Loaded += async (_, _) => await RefreshBadgeAsync();
    }

    private IBoardWorkspaceHost? Host()
    {
        for (Window? owner = board.Owner; owner is not null; owner = owner.Owner)
            if (owner is IBoardWorkspaceHost host) return host;
        return null;
    }

    private async Task RefreshBadgeAsync()
    {
        try
        {
            var unread = (await client.NotificationsAsync(board.Token)).UnreadCount;
            Count.Text = unread > 99 ? "99+" : unread.ToString();
            Badge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetHelpText(Notifications, $"읽지 않은 알림 {unread}개");
        }
        catch (OperationCanceledException) { }
        catch (Exception) { Badge.Visibility = Visibility.Collapsed; AutomationProperties.SetHelpText(Notifications, "알림 목록 열기"); }
    }
}
