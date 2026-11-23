using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;

namespace Semojum.Desktop.Dialogs;

/// <summary>The server receives a request for operator review, not an immediate account or credit change.</summary>
internal sealed class InstitutionRequestDialog : InstitutionFormDialog
{
    internal TextBox First { get; }
    internal TextBox Second { get; } = Entry("받을 사람 메일", 100);
    internal IReadOnlyList<RadioButton> ChoicesShown { get; }
    internal Button Submit { get; }
    internal TextBlock EstimateAmount { get; } = Value("—");
    internal TextBlock EstimateNote { get; } = Value("(부가세 포함)");
    internal static readonly TimeSpan EstimateDelay = TimeSpan.FromMilliseconds(300);
    private readonly Func<long, CancellationToken, Task<InstitutionCreditEstimate>>? estimate;
    private CancellationTokenSource? estimateRun;
    internal Task EstimateTask { get; private set; } = Task.CompletedTask;

    internal InstitutionRequestDialog(bool accountRequest, Func<string?, InstitutionRequestDetails, Task> submit, Func<long, CancellationToken, Task<InstitutionCreditEstimate>>? estimate = null)
        : base(accountRequest ? "계정 발급 요청" : "크레딧 추가 요청", accountRequest ? 640 : 600, accountRequest ? 318 : 250,
            accountRequest ? "institution-account-request-close.svg" : "institution-credit-request-close.svg")
    {
        this.estimate = estimate;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Semojum.UI.Common;component/Controls/CompactRadio.xaml", UriKind.Relative) });
        var card = Card();
        First = Entry(accountRequest ? "별칭 (50자)" : "크레딧 수", accountRequest ? 50 : 14);
        if (accountRequest)
        {
            Field(card, "별칭 (50자)", First); Field(card, "받을 사람 메일", Second);
            Second.ToolTip = "요청하면 운영자 확인 뒤 이 메일 주소로 첫 비밀번호를 보냅니다.";
        }
        else
        {
            Field(card, "크레딧 수", Quantity(First, 1, int.MaxValue));
            First.TextChanged += (_, _) => EstimateTask = EstimateAsync();
            Closed += (_, _) => estimateRun?.Cancel();
            var price = new StackPanel { Orientation = Orientation.Horizontal };
            EstimateAmount.FontWeight = EstimateNote.FontWeight = FontWeights.SemiBold;
            EstimateNote.Margin = new(4, 0, 0, 0); EstimateNote.Visibility = Visibility.Collapsed;
            price.Children.Add(EstimateAmount); price.Children.Add(EstimateNote);
            Field(card, "예상 금액", price);
        }
        string[] choices = accountRequest ? ["기관 할당 공유", "계정별 한도 지정", "크레딧 없음 (읽기만)"] : ["기관 명의", "개인 명의", "발행 안 함"];
        Panel options = accountRequest ? new StackPanel() : new WrapPanel { Orientation = Orientation.Horizontal };
        ChoicesShown = choices.Select((label, index) =>
        {
            var radio = new RadioButton { Content = label, Height = 20, IsChecked = index == 0,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = index == 0 ? new(0) : accountRequest ? new(0, 8, 0, 0) : new(16, 0, 0, 0) };
            options.Children.Add(radio); return radio;
        }).ToArray();
        Field(card, accountRequest ? "쓸 수 있는 크레딧" : "세금계산서", options, alignTop: accountRequest);
        Dismiss("취소");
        Submit = FooterAction("요청", () => RunAsync(async () =>
        {
            string? message = null;
            InstitutionRequestDetails details;
            if (accountRequest)
            {
                if (string.IsNullOrWhiteSpace(First.Text) || !InstitutionClient.IsReceiptEmail(Second.Text.Trim()))
                    throw new ArgumentException("별칭과 올바른 이메일을 입력해 주세요.");
                var scope = ChoicesShown[0].IsChecked == true ? "SHARED" : ChoicesShown[2].IsChecked == true ? "NONE" : null;
                if (scope is null) message = "쓸 수 있는 크레딧: 계정별 한도 지정";
                details = new(AccountAlias: First.Text.Trim(), RecipientEmail: Second.Text.Trim(), CreditScope: scope);
            }
            else
            {
                if (Credits() is not { } amount) throw new ArgumentException("요청할 크레딧을 양의 정수로 입력해 주세요.");
                var invoice = ChoicesShown[1].IsChecked == true ? "PERSONAL" : ChoicesShown[2].IsChecked == true ? "NONE" : "ORG";
                details = new(CreditAmount: amount, InvoiceType: invoice);
            }
            await submit(message, details); DialogResult = true;
        }), true);
    }
    private long? Credits() => int.TryParse(First.Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var amount) && amount > 0 ? amount : null;

    private async Task EstimateAsync()
    {
        estimateRun?.Cancel();
        ShowEstimate(null);
        if (estimate is null || Lifetime.IsCancellationRequested || Credits() is not { } credits) return;
        using var run = estimateRun = new CancellationTokenSource();
        try
        {
            await Task.Delay(EstimateDelay, run.Token);
            var value = await estimate(credits, run.Token);
            if (!run.IsCancellationRequested && value.Credits == credits && value.AmountKrw >= 0) ShowEstimate(value);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception) { if (!run.IsCancellationRequested) ShowEstimate(null); }
        finally { if (ReferenceEquals(estimateRun, run)) estimateRun = null; }
    }
    private void ShowEstimate(InstitutionCreditEstimate? quote)
    {
        EstimateAmount.Text = quote is null ? "—" : $"{quote.AmountKrw:N0}원";
        EstimateAmount.ToolTip = quote is null ? null : $"크레딧당 {quote.UnitPriceKrw:0.####}원";
        EstimateNote.Visibility = quote is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
