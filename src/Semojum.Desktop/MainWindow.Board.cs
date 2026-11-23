using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop;

public partial class MainWindow : IBoardWorkspaceHost
{
    void IBoardWorkspaceHost.ReturnToEditor(BoardDialog board) => ReturnFromBoard(board, this);

    void IBoardWorkspaceHost.OpenBoardAccountMenu(BoardDialog board, Button anchor)
    {
        if (api.Account is not { } account) return;
        var (menu, page, logout) = AccountContextMenu.Create(anchor, account, account.DisplayName);
        page.Click += async (_, _) =>
        {
            for (var owner = board.Owner; owner is not null; owner = owner.Owner)
                if (owner is MyPageWindow) { ReturnFromBoard(board, owner); return; }
            await OpenFromBoard("usage");
        };
        logout.Click += async (_, _) => await OpenFromBoard("logout");
        OpenRibbonMenu(anchor, menu);

        async Task OpenFromBoard(string command)
        {
            if (!ReturnFromBoard(board, this)) return;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await ExecuteAsync(command);
        }
    }

    private static bool ReturnFromBoard(BoardDialog board, Window target)
    {
        var path = new List<Window>();
        for (Window? window = board; window != target; window = window.Owner)
        {
            if (window is null) return false;
            path.Add(window);
        }
        foreach (var window in path)
        {
            window.Close();
            if (window.IsVisible) return false;
        }
        target.Activate();
        return true;
    }
}
