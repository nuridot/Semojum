using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Semojum.Desktop.Dialogs;

internal sealed class InstitutionAccountDetailDialog : InstitutionFormDialog
{
    internal Button LockButton { get; }
    internal Button SaveButton { get; }
    internal Button RecentJobs { get; }
    internal DataGrid Jobs { get; }
    internal TextBox AliasBox { get; } = Entry("별칭", 50);
    internal TextBox EmailBox { get; } = Entry("메일", 100);
    internal TextBox LimitBox { get; } = Entry("크레딧 한도", 25);

    internal InstitutionAccountDetailDialog(InstitutionAccount account, InstitutionAccountJobs jobs, Func<bool, Task> setLocked,
        Func<string?, string?, long?, Task> save)
        : base(account.LoginId, 560, 462, "institution-account-detail-close.svg")
    {
        var fields = Card();
        AliasBox.Text = account.Alias ?? ""; EmailBox.Text = account.Email ?? "";
        LimitBox.Text = account.CreditLimit?.ToString("N0", CultureInfo.CurrentCulture) ?? "";
        LimitBox.ToolTip = "비워 두면 제한 없음, 0은 크레딧 사용 불가";
        Field(fields, "별칭", AliasBox); Field(fields, "메일", EmailBox); Field(fields, "크레딧 한도", Quantity(LimitBox, 0, long.MaxValue));
        // Extended account responses always include this month's usage, even when the address and limit are null.
        var canEdit = account.MonthUsedCredits.HasValue;
        fields.IsEnabled = canEdit;
        var facts = Card();
        var locked = account.Status == "INACTIVE";
        var status = Value(InstitutionStatusTextConverter.Label(account.Status, "account"));
        if (locked && InstitutionUsage.InKorea(account.LockedAt) is { } lockedAt)
            status.Text += " · " + lockedAt.ToString("MM-dd", CultureInfo.InvariantCulture) + " 부터";
        status.ToolTip = "마지막 로그인 · " + InstitutionUsage.LastSeen(account.LastLoginAt, DateTimeOffset.Now, withTime: true);
        Field(facts, "상태", status, true);
        var created = InstitutionUsage.InKorea(account.CreatedAt)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Field(facts, "만든 날", Value(created ?? "—"), true);
        var used = Value(account.MonthUsedCredits?.ToString("N0") ?? "—");
        used.ToolTip = $"누적 사용 {account.UsedCredits:N0} 크레딧"; Field(facts, "이달 사용", used, true);
        var latest = jobs.Items.FirstOrDefault();
        var day = InstitutionUsage.InKorea(latest?.FinishedAt ?? latest?.StartedAt)?.ToString("MM-dd", CultureInfo.InvariantCulture);
        var recent = latest is null ? "—" : string.IsNullOrEmpty(day) ? latest.FileName : $"{latest.FileName} · {day}";
        var jobLabel = Value(recent); jobLabel.TextWrapping = TextWrapping.NoWrap; jobLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        RecentJobs = new Button { Content = jobLabel, Height = 28, MinHeight = 0, MinWidth = 0, Padding = new(0), Margin = new(0),
            Template = EditorModalDesign.ButtonTemplate(typeof(Button)), HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, BorderThickness = new(0), ToolTip = latest is null ? null : "최근 작업 목록 열기 또는 닫기", IsEnabled = latest is not null };
        Field(facts, "최근 작업", RecentJobs, true);
        var jobBackground = FigmaDialogElements.Brush("#F9F9F9");
        Jobs = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.None, BorderThickness = new(0), Background = jobBackground,
            RowBackground = jobBackground, AlternatingRowBackground = jobBackground, GridLinesVisibility = DataGridGridLinesVisibility.None, RowHeight = 32,
            Style = (Style)FindResource("InstitutionJobsGrid"), ItemsSource = jobs.Items, MinHeight = 80, MaxHeight = 130 };
        var baseCell = (Style)Jobs.Style.Setters.OfType<Setter>().Single(s => s.Property == DataGrid.CellStyleProperty).Value;
        var nameCell = new Style(typeof(DataGridCell), baseCell); nameCell.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 0, 8, 0)));
        var detailCell = new Style(typeof(DataGridCell), baseCell); detailCell.Setters.Add(new Setter(Control.ForegroundProperty, FigmaDialogElements.Brush("#5F5F5F")));
        var nameText = new Style(typeof(TextBlock)); nameText.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        nameText.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding("FileName")));
        Jobs.Columns.Add(new DataGridTextColumn { Header = "파일", Binding = new Binding("FileName"), Width = new(1, DataGridLengthUnitType.Star), MinWidth = 80, CellStyle = nameCell, ElementStyle = nameText });
        Jobs.Columns.Add(new DataGridTextColumn { Header = "쪽", Binding = new Binding("TotalPages") { StringFormat = "{0}쪽" }, Width = 70, CellStyle = detailCell });
        Jobs.Columns.Add(new DataGridTextColumn { Header = "크레딧", Binding = new Binding("Credits") { TargetNullValue = "— 크레딧", StringFormat = "{0} 크레딧" }, Width = 118, CellStyle = detailCell });
        AutomationProperties.SetName(Jobs, "해당 계정의 최근 작업");
        Jobs.Visibility = Visibility.Collapsed; facts.Children.Add(Jobs);
        RecentJobs.Click += (_, _) => { Jobs.Visibility = Jobs.IsVisible ? Visibility.Collapsed : Visibility.Visible; if (Jobs.IsVisible) Jobs.BringIntoView(); };

        LockButton = FooterAction(locked ? "잠금 해제" : "잠금", () => RunAsync(async () =>
        {
            var message = locked ? "이 계정의 잠금을 해제하시겠습니까?" : "계정을 잠그면 로그인 세션이 종료되고 진행 중인 작업이 취소될 수 있습니다. 계속하시겠습니까?";
            if (NoticeDialog.Choose(this, message, "변경은 서버에 즉시 적용됩니다.", ["계속", "취소"], error: !locked) != 0) return;
            await setLocked(!locked); DialogResult = true;
        }), left: true);
        LockButton.IsEnabled = !account.Self && account.Status is "ACTIVE" or "INACTIVE";
        if (!LockButton.IsEnabled) LockButton.ToolTip = account.Self ? "본인 계정은 여기에서 잠글 수 없습니다." : "현재 계정 상태에서는 잠금 변경을 할 수 없습니다.";
        Dismiss("닫기");
        SaveButton = FooterAction("저장", () => RunAsync(async () =>
        {
            var email = EmailBox.Text.Trim();
            if (email.Length > 0 && !InstitutionClient.IsReceiptEmail(email)) throw new ArgumentException("올바른 이메일을 입력해 주세요.");
            long? limit = null;
            if (!string.IsNullOrWhiteSpace(LimitBox.Text))
            {
                if (!long.TryParse(LimitBox.Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var amount) || amount < 0)
                    throw new ArgumentException("한 달 크레딧 한도는 0 이상의 정수로 입력해 주세요.");
                limit = amount;
            }
            var alias = AliasBox.Text.Trim();
            await save(alias.Length == 0 ? null : alias, email.Length == 0 ? null : email, limit); DialogResult = true;
        }), true);
        SaveButton.IsEnabled = canEdit;
        if (!canEdit) { SaveButton.ToolTip = "이 서버에서 전체 계정 정보를 받을 수 없어 저장할 수 없습니다."; ToolTipService.SetShowOnDisabled(SaveButton, true); }
    }
}
