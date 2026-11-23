using System.Windows;
using System.Windows.Controls;

namespace Semojum.Desktop.Dialogs;

public sealed class AboutDialog : FormDialogBase
{
    public TextBlock EngineVersion { get; } = new() { Text = "확인 중…", FontSize = 14 };
    public AboutDialog(string version, Func<Task<string>> engineVersion)
        : base("누리닷 정보", 440, 302, EditorModalDesign.Icon("form-close", assemblyName: "Semojum.UI.Common"))
    {
        AlignLabelBaseline();
        var rows = new StackPanel();
        Fields.Children.Add(Card(rows, new(16, 12, 16, 12)));
        void Info(string label, FrameworkElement value)
        {
            var row = new DockPanel { Height = 28, Margin = new(0, rows.Children.Count == 0 ? 0 : 12, 0, 0) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 108,
                FontSize = 14,
                Foreground = EditorModalDesign.Color("#5F5F5F"),
                VerticalAlignment = VerticalAlignment.Center
            });
            value.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(value); rows.Children.Add(row);
        }
        Info("버전", new TextBlock { Text = "V" + version.TrimStart('v', 'V'), FontSize = 14 });
        var engine = new StackPanel { Orientation = Orientation.Horizontal };
        var prefix = new TextBlock { Text = "v", FontSize = 14, Visibility = Visibility.Collapsed };
        engine.Children.Add(prefix); engine.Children.Add(EngineVersion);
        Info("점역 엔진", engine);
        Info("운영 기관", new TextBlock { Text = "누리닷", FontSize = 14 });
        Info("연락처", new TextBlock { Text = "contact@nuridot.com", FontSize = 14 });
        FooterButton("확인", Close, true);
        Loaded += async (_, _) =>
        {
            try
            {
                var value = await engineVersion();
                if (Lifetime.IsCancellationRequested) return;
                EngineVersion.Text = value;
                prefix.Visibility = Version.TryParse(value, out _) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                if (!Lifetime.IsCancellationRequested) EngineVersion.Text = "확인 불가 · 엔진 연결을 확인해 주세요.";
            }
        };
    }
}
