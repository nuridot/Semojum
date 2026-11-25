using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

internal sealed class SignupAgreements : Border
{
    // Versions of the documents displayed by LegalDocumentDialog.
    internal const string TermsVersion = "2026-11-09", PrivacyVersion = "2026-11-09";
    private readonly CheckBox terms = new(), privacy = new(), marketing = new();
    internal bool RequiredAccepted => terms.IsChecked == true && privacy.IsChecked == true;
    internal string? MarketingVersion => marketing.IsChecked == true ? "2026-11-25" : null;

    internal SignupAgreements()
    {
        var agreements = new StackPanel();
        var checkStyle = AccountFormElements.CheckStyle();
        var all = new CheckBox { Content = "약관에 모두 동의합니다", Style = checkStyle };
        agreements.Children.Add(all);
        void Agreement(CheckBox check, string label, LegalDocumentKind kind)
        {
            var row = new DockPanel { Margin = new(0, 8, 0, 0) };
            var view = new Button { Content = "보기", FontSize = 12, Height = 20, MinWidth = 0, MinHeight = 0, Padding = new(0), Margin = new(0),
                Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = AccountFormElements.Ink("#003E92"), Template = FigmaDialogElements.SurfaceTemplate(0) };
            AutomationProperties.SetName(view, label + " 보기");
            void SyncAll() => all.IsChecked = terms.IsChecked == true && privacy.IsChecked == true && marketing.IsChecked == true;
            view.Click += (_, _) =>
            {
                var dialog = new LegalDocumentDialog(kind) { Owner = Window.GetWindow(this) };
                dialog.ShowDialog();
                if (dialog.MarketingChoice is { } choice) { check.IsChecked = choice; SyncAll(); }
            };
            DockPanel.SetDock(view, Dock.Right); row.Children.Add(view);
            check.Content = label; check.Style = checkStyle; row.Children.Add(check); agreements.Children.Add(row);
            check.Click += (_, _) => SyncAll();
        }
        Agreement(terms, "[필수] 이용약관 동의", LegalDocumentKind.Terms);
        Agreement(privacy, "[필수] 개인정보 처리방침 동의", LegalDocumentKind.Privacy);
        Agreement(marketing, "[선택] 광고성 정보 수신 동의", LegalDocumentKind.Marketing);
        all.Click += (_, _) => terms.IsChecked = privacy.IsChecked = marketing.IsChecked = all.IsChecked;
        Background = AccountFormElements.Ink("#F9F9F9"); CornerRadius = new(4); Margin = new(0,16,0,0); Padding = new(12); Child = agreements;
    }
}
