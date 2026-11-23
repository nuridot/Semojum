using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public static class AccountContextMenu
{
    public static (ContextMenu Menu, MenuItem Page, MenuItem Logout) Create(FrameworkElement anchor, AccountIdentity? account, string name)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom,
            HorizontalOffset = anchor.ActualWidth - 220, FontFamily = (FontFamily)anchor.FindResource("UiFont") };
        menu.Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Usage.Wpf;component/Styles/AccountMenu.xaml", UriKind.Relative) });
        menu.Style = (Style)menu.FindResource("IdentityMenu");
        var role = account switch
        {
            { Role: "ROLE_ORG_ADMIN" } => "기관 관리자",
            { Role: "ROLE_ADMIN" } => "운영자",
            { MemberType: "PERSONAL" } => "개인 회원",
            _ => "점역사"
        };
        var identity = new StackPanel();
        identity.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name });
        identity.Children.Add(new TextBlock { Text = $"{account?.LoginId} · {role}", FontSize = 12,
            Foreground = EditorModalDesign.Color("#5F5F5F"), TextTrimming = TextTrimming.CharacterEllipsis });
        menu.Items.Add(new MenuItem { Header = identity, Style = (Style)menu.FindResource("IdentityMenuHeader"), IsEnabled = false });
        var actionStyle = (Style)menu.FindResource("IdentityMenuAction");
        var page = new MenuItem { Header = "마이페이지", Style = actionStyle, Icon = EditorModalDesign.Icon("mypage-user", assemblyName: "Semojum.Feature.Usage.Wpf") };
        var logout = new MenuItem { Header = "로그아웃", Style = actionStyle, Foreground = EditorModalDesign.Color("#C42B1C"),
            Icon = new Border { Width = 12, Height = 12, CornerRadius = new(1), BorderThickness = new(1.3), BorderBrush = EditorModalDesign.Color("#5F5F5F") } };
        menu.Items.Add(page);
        menu.Items.Add(logout);
        return (menu, page, logout);
    }
}
