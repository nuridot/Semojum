using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Semojum.Core.Integration;
using SharpVectors.Converters;

namespace Semojum.Desktop.Dialogs;

internal sealed class SocialLoginButtons : StackPanel
{
    private const string Assets = "pack://application:,,,/Semojum.Feature.Account.Wpf;component/Assets/Figma/";
    internal SocialLoginButtons()
    {
        // The two full-width brand images extend 2 DIPs beyond the 298 DIP input fields in A1.
        Margin = new(0,0,-2,0); Visibility = Visibility.Collapsed;
    }

    internal void ShowRetry(Func<Task> reload)
    {
        Children.Clear(); Visibility = Visibility.Visible;
        var retry = AccountFormElements.ActionButton("소셜 로그인 목록 다시 불러오기", false);
        retry.Margin = new(0,16,2,0);
        retry.Click += async (_, _) => { retry.IsEnabled = false; await reload(); };
        Children.Add(retry);
    }

    internal void ShowProviders(IReadOnlyList<SocialClient> providers, Func<SocialClient, Task> signIn)
    {
        Children.Clear();
        Visibility = providers.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var client in providers)
        {
            var (label, height) = client.Provider switch
            {
                SocialProvider.Google => ("Google로 계속하기", 40),
                SocialProvider.Kakao => ("카카오 로그인", 41),
                SocialProvider.Naver => ("네이버 로그인", 39),
                _ => throw new ArgumentOutOfRangeException(nameof(providers))
            };
            var button = AccountFormElements.ActionButton(label, false);
            button.Height = height;
            button.Margin = new(0, Children.Count == 0 ? 16 : 8, client.Provider == SocialProvider.Google ? 2 : 0, 0);
            button.Template = FigmaDialogElements.SurfaceTemplate(8, stretchContent: false, useControlInsets: true);
            if (client.Provider == SocialProvider.Google)
            {
                button.BorderBrush = AccountFormElements.Ink("#D1D1D1");
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(Svg("login-google.svg", 18, 18));
                content.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new(8,0,0,0) });
                button.Content = content;
            }
            else
            {
                button.HorizontalAlignment = HorizontalAlignment.Left;
                button.Width = 300;
                button.Background = button.BorderBrush = Brushes.Transparent;
                button.BorderThickness = new(0);
                button.Content = client.Provider == SocialProvider.Kakao ? Svg("login-kakao.svg", 300, height)
                    : new Image { Width = 300, Height = height, Stretch = Stretch.Fill, Source = new BitmapImage(new Uri(Assets + "login-naver.png")) };
            }
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetAutomationId(button, "LoginSocial" + client.Provider);
            button.Click += async (_, _) => await signIn(client);
            Children.Add(button);
        }
        if (providers.Count == 0) return;
        var divider = new Grid { Height = 16, Margin = new(0,16,2,0) };
        divider.ColumnDefinitions.Add(new());
        divider.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        divider.ColumnDefinitions.Add(new());
        foreach (var column in new[] { 0, 2 })
        {
            var line = new Border { Height = 1, Background = AccountFormElements.Ink("#EAEAEA") };
            Grid.SetColumn(line, column); divider.Children.Add(line);
        }
        var title = new TextBlock { Text = "또는 아이디로", FontSize = 12, Foreground = AccountFormElements.Ink("#8A8A8A"), Margin = new(12,0,12,0) };
        Grid.SetColumn(title, 1); divider.Children.Add(title); Children.Add(divider);
    }

    private static SvgViewbox Svg(string name, double width, double height) =>
        new() { Width = width, Height = height, Stretch = Stretch.Fill, Source = new Uri(Assets + name) };
}
