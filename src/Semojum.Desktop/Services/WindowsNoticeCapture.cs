using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsNoticeCapture
{
    internal static void Register(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "observed-notices.jsonl");
        File.WriteAllText(path, "");
        var actions = Path.Combine(directory, "observed-notice-actions.jsonl");
        File.WriteAllText(actions, "");
        void Action(string message, string kind, string? value) =>
            File.AppendAllText(actions, JsonSerializer.Serialize(new { message, kind, value }) + Environment.NewLine);
        EventManager.RegisterClassHandler(typeof(NoticeDialog), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, e) =>
            {
                if (sender is not NoticeDialog window || !ReferenceEquals(e.OriginalSource, window)) return;
                var buttons = window.ActionButtons.OrderBy(Grid.GetColumn).Select(button => new {
                    Text = button.Content as string, button.IsDefault, button.IsCancel, button.IsEnabled,
                    TabIndex = KeyboardNavigation.GetTabIndex(button)
                });
                File.AppendAllText(path, JsonSerializer.Serialize(new {
                    window.MessageText, window.DetailText, window.Width, window.Height, Buttons = buttons
                }) + Environment.NewLine);
                window.AddHandler(Button.ClickEvent, new RoutedEventHandler((_, click) =>
                {
                    if (click.OriginalSource is Button button && window.ActionButtons.Contains(button))
                        Action(window.MessageText, "click", button.Content as string);
                }), true);
                window.PreviewKeyDown += (_, key) =>
                {
                    if (key.Key is Key.Enter or Key.Escape) Action(window.MessageText, "key", key.Key.ToString());
                };
                window.Closed += (_, _) => Action(window.MessageText, "closed", window.Choice.ToString());
            }));
    }
}
