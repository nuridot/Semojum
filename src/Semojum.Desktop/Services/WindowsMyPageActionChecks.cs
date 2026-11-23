using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsMyPageActionChecks
{
    public static async Task RunAsync(MyPageWindow owner, MyPageView view, SemojumApiClient api, Action<bool, string> check)
    {
        Button Entry(string caption) => Children(view).OfType<Button>().Single(b => Equals(b.Content, caption));
        Button Named(string name) => (Button)view.FindName(name);
        static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var shots = Environment.GetEnvironmentVariable("MYPAGE_CHILD_CAPTURE");

        async Task Open(string title, double width, double height, Button launch, string key, Func<Window, Task> exercise)
        {
            await InspectModal(owner, () => Click(launch), title, async w =>
            {
                await Until(() => w.IsLoaded && Children(w).OfType<Button>().Any(b => Equals(b.Content, key == "E1-3" ? "닫기" : "취소") && b.IsEnabled));
                w.UpdateLayout();
                check(w.Width == width && w.Height == height && !Children(w).OfType<TabControl>().Any(),
                    $"{key} opens the distinct {width}x{height} Figma form from My Page");
                var actionWidth = key == "E1-3" ? 99 : 88;
                check(Children(w).OfType<Button>().Any(b => b.Width == actionWidth && b.Height == 32 && b.IsDefault), $"{key} has the {actionWidth}x32 primary action");
                if (shots is not null) await WindowsSmoke.RenderElementAsync(w, Path.Combine(shots, key + ".png"));
                await exercise(w);
            });
        }

        var payments = (DataGrid)view.FindName("PaymentsRows");
        payments.BringIntoView(); payments.UpdateLayout();
        var invoiceButtons = Children(payments).OfType<Button>().Where(b => Equals(b.Content, "청구서")).ToArray();
        check(invoiceButtons.Length == 2 && invoiceButtons.Count(b => b.IsEnabled) == 1, "Only a payment with an issued invoice can open the E1-3 invoice");
        await Open("청구서 · 2026-11-01", 560, 490, invoiceButtons.Single(b => b.IsEnabled), "E1-3", w =>
        {
            check(HasText(w, "Standard 요금제") && HasText(w, "50,000원") && HasText(w, "2026-11-01")
                && Children(w).OfType<TextBlock>().Count(t => t.Text == "—") == 5, "The invoice shows the settled date in Korea and actual order values, leaving unsupported fields unfilled");
            Click(Action(w, "닫기")); return Task.CompletedTask;
        });
        await Open("요금제 업그레이드", 760, 426, Entry("요금제 업그레이드"), "E2", async w =>
        {
            var cards = Children(w).OfType<RadioButton>().ToArray(); await Until(() => Action(w, "주문안 저장").IsEnabled);
            check(cards.Length == 4 && cards.Any(c => AutomationProperties.GetName(c) == "Standard" && !c.IsEnabled)
                && cards.Any(c => AutomationProperties.GetName(c) == "Premium" && c.IsChecked == true)
                && HasText(w, "297,000원 / 월"), "E2 shows the returned catalog, current subscription and next eligible plan");
            Click(Action(w, "취소"));
        });
        await Open("추가 크레딧 구매", 640, 522, Entry("＋ 추가 크레딧 구매"), "E3", async w =>
        {
            await Until(() => Action(w, "주문안 저장").IsEnabled);
            check(HasText(w, "10% 할인") && HasText(w, "30% 할인") && HasText(w, "257,400원") && HasText(w, "23,400원"),
                "E3 quantities, discount badges, VAT and amount reflect the fixture quotes");
            Children(w).OfType<RadioButton>().Single(c => AutomationProperties.GetName(c) == "직접 입력").IsChecked = true;
            var input = Input<TextBox>(w, "구매할 크레딧");
            input.Text = "2000"; input.Text = "2500";
            check(!Action(w, "주문안 저장").IsEnabled, "E3 invalidates the previous quote while a new quantity is pending");
            await Until(() => HasText(w, "7,150원") && Action(w, "주문안 저장").IsEnabled);
            Click(Action(w, "주문안 저장"));
        });
        await Open("이메일 변경", 520, 262, Named("EmailAction"), "E4", async w =>
        {
            check(HasText(w, "fixture@example.test") && Input<TextBox>(w, "인증 번호").MaxLength == 6, "E4 shows the account email and six-digit verification field");
            var address = Input<TextBox>(w, "새 이메일"); address.Text = "new@example.test";
            await InspectModal(w, () => Click(Action(w, "인증 번호 받기")), "이메일 변경 본인 확인", async prompt =>
            {
                await Until(() => prompt.IsLoaded); Input<PasswordBox>(prompt, "지금 비밀번호").Password = "test-current"; Click(Action(prompt, "확인"));
            });
            await Until(() => HasText(w, "인증 번호를 5분", contains: true));
            var code = Input<TextBox>(w, "인증 번호"); code.Text = "123456"; address.Text = "changed@example.test";
            check(code.Text.Length == 0, "Changing the address clears the previous verification code");
            Click(Action(w, "변경"));
            await Until(() => HasText(w, "6자리 인증 번호", contains: true));
            Click(Action(w, "취소"));
        });
        await Open("비밀번호 변경", 520, 274, Named("PasswordAction"), "E5", async w =>
        {
            var passwords = Children(w).OfType<PasswordBox>().ToArray();
            check(passwords.Length == 3 && passwords.All(f => f.Height == 32), "E5 has three masked 32px fields");
            passwords[0].Password = "test-current"; passwords[1].Password = "test-new-password"; passwords[2].Password = "mismatch";
            Click(Action(w, "변경")); await Until(() => HasText(w, "일치해야", contains: true));
            Click(Action(w, "취소"));
        });
        await Open("회원 탈퇴", 520, 278, Named("LeaveAction"), "E6", async w =>
        {
            check(Children(w).OfType<CheckBox>().Single().IsChecked == false && HasText(w, "비식별", contains: true), "E6 explains the actual retention behavior and starts with unchecked consent");
            Click(Action(w, "탈퇴")); await Until(() => HasText(w, "탈퇴 안내", contains: true)); Click(Action(w, "취소"));
        });

        var saved = new AccountChangeDialog(api, AccountChangeDialog.Section.Password) { Owner = owner };
        await InspectModal(owner, () => saved.ShowDialog(), "비밀번호 변경", async w =>
        {
            await Until(() => Action(w, "변경").IsEnabled);
            Input<PasswordBox>(w, "지금 비밀번호").Password = "test-current";
            Input<PasswordBox>(w, "새 비밀번호").Password = Input<PasswordBox>(w, "새 비밀번호 확인").Password = "test-new-password";
            Click(Action(w, "변경"));
        });
        check(saved.DialogResult == true, "A confirmed password change returns the sign-in-again result");
    }

    private static Button Action(Window w, string text) => Children(w).OfType<Button>().Single(b => Equals(b.Content, text));
    private static T Input<T>(Window w, string label) where T : Control =>
        Children(w).OfType<T>().Single(c => AutomationProperties.GetName(c) == label);
    private static bool HasText(Window w, string text, bool contains = false) =>
        Children(w).OfType<TextBlock>().Any(c => contains ? c.Text.Contains(text, StringComparison.Ordinal) : c.Text == text);
    private static IEnumerable<DependencyObject> Children(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var item in Children(child)) yield return item;
        }
    }
    private static async Task Until(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (!predicate()) throw new InvalidOperationException("My Page action did not reach its expected state.");
    }
    private static async Task InspectModal(Window owner, Action open, string title, Func<Window, Task> inspect)
    {
        var operation = owner.Dispatcher.InvokeAsync(async () =>
        {
            await Until(() => owner.OwnedWindows.Cast<Window>().Any(w => w.IsVisible && w.Title == title));
            var modal = owner.OwnedWindows.Cast<Window>().Single(w => w.IsVisible && w.Title == title);
            try { await inspect(modal); }
            finally { if (modal.IsVisible) modal.Close(); }
        }, DispatcherPriority.ApplicationIdle);
        open();
        await operation.Task.Unwrap();
    }
}
