using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Semojum.Desktop.Dialogs;

internal sealed class ProofModelSelector : StackPanel
{
    private static readonly (string Code, string Label)[] Levels =
        [("MINERU", "기본"), ("HAIKU", "중급"), ("SONNET", "고급"), ("OPUS", "최고급")];

    public string Code { get; private set; }
    public string Label => Levels.First(level => level.Code == Code).Label;

    public ProofModelSelector(string? initialCode = null)
    {
        Code = Levels.Any(level => level.Code == initialCode) ? initialCode! : Levels[0].Code;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.Feature.Documents.Wpf;component/ProofChoice.xaml", UriKind.Relative) });
        var group = Guid.NewGuid().ToString();
        foreach (var (code, label) in Levels)
        {
            var radio = new RadioButton
            {
                GroupName = group, Content = label, Style = (Style)FindResource("ProofChoice"),
                Margin = new(0, Children.Count == 0 ? 0 : 4, 0, 0), IsChecked = code == Code
            };
            AutomationProperties.SetName(radio, label);
            radio.Checked += (_, _) => Code = code;
            Children.Add(radio);
        }
    }
}
