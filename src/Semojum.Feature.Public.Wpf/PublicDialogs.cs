using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using Semojum.Core.Integration;
using SharpVectors.Converters;
using static Semojum.Desktop.Dialogs.FigmaDialogElements;

namespace Semojum.Desktop.Dialogs;

public sealed class PublicNoticesDialog : CompactDialog
{
    // Figma tabs and card pills: the server's category code, its label, tab width, pill width and pill color (A2 263:10854–10884).
    private static readonly (string Code, string Label, double Tab, double Pill, string Color)[] Categories =
        [("MAINTENANCE", "점검", 44, 40, "#9D5D00"), ("RELEASE", "새 판", 51, 47, "#005FB8"), ("MATERIAL", "자료", 44, 40, "#005FB8")];

    public PublicNoticesDialog(SemojumApiClient api) : base("공지사항", 600, 488, popover: true)
    {
        var layout = PublicDialogDesign.Frame(this, "공지사항", "public-notice-close.svg", "PublicNoticesClose", out var actions, "Semojum.Feature.Public.Wpf"); Body(layout);
        var body = new DockPanel { Margin = new(16, 12, 16, 0) }; Grid.SetRow(body, 1); layout.Children.Add(body);
        var categories = new StackPanel { Orientation = Orientation.Horizontal, Height = 24, Margin = new(0, 0, 0, 16) };
        DockPanel.SetDock(categories, Dock.Top); body.Children.Add(categories);
        var panel = new StackPanel(); body.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        // The app filters the loaded notices; a notice without a category appears only under 「전체」 (null).
        (string Title, string Body, string Date, string? Category)[]? notices = null; string? shown = null;
        void Render()
        {
            foreach (Button chip in categories.Children)
            {
                var selected = Equals(chip.Tag, shown);
                chip.Background = Brush(selected ? "#EFF6FC" : "#FFFFFF"); chip.BorderBrush = Brush(selected ? "#005FB8" : "#D1D1D1"); chip.Foreground = Brush(selected ? "#1A1A1A" : "#5F5F5F");
                AutomationProperties.SetItemStatus(chip, selected ? "선택됨" : "선택 안 됨");
            }
            if (notices is null) return;
            var visible = notices.Where(n => shown is null || n.Category == shown).ToArray();
            panel.Children.Clear();
            if (visible.Length == 0) panel.Children.Add(new TextBlock { Text = "현재 표시할 공지가 없습니다." });
            foreach (var card in visible.Select((n, i) => CreateNotice(n.Title, n.Body, n.Date, n.Category, i == 0))) panel.Children.Add(card);
        }
        foreach (var (code, label, width) in new[] { ((string?)null, "전체", 44d) }.Concat(Categories.Select(c => ((string?)c.Code, c.Label, c.Tab))))
        {
            var chip = new Button { Content = label, Tag = code, Width = width, Height = 24, MinWidth = 0, MinHeight = 0, Padding = new(0), Margin = new(0, 0, 8, 0), Template = SurfaceTemplate(10) };
            chip.Click += (_, _) => { shown = code; Render(); };
            categories.Children.Add(chip);
        }
        Render();
        var loading = false;
        Closing += (_, e) => { if (loading) { e.Cancel = false; Lifetime.Cancel(); } };
        async Task LoadAsync()
        {
            loading = true;
            try
            {
                var result = await api.GetPublicAsync("/api/public/notices", Lifetime.Token);
                if (Lifetime.IsCancellationRequested) return;
                notices = result.EnumerateArray().Select(n => (Title: n.GetProperty("title").GetString() ?? "", Body: n.GetProperty("body").GetString() ?? "", Date: NoticeDate(n),
                    Category: n.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null)).ToArray();
                Render();
            }
            finally { loading = false; }
        }
        // Keep the custom title-bar close action interactive while the server is slow.
        // Disable duplicate requests without disabling the entire dialog.
        async Task LoadSafelyAsync()
        {
            if (loading) return;
            Status.Text = "";
            try { await LoadAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Status.Text = ex.Message; }
        }
        var close = Button("닫기", () => { Close(); return Task.CompletedTask; }, true); PublicDialogDesign.StyleAction(close, true); actions.Children.Add(close);
        Loaded += async (_, _) => await LoadSafelyAsync();
    }

    private static string NoticeDate(JsonElement notice)
    {
        if (!notice.TryGetProperty("createdAt", out var value) || value.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)) return "";
        return date.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy. MM. dd.", CultureInfo.InvariantCulture);
    }

    private static Expander CreateNotice(string title, string body, string date, string? category, bool expanded)
    {
        var header = new Grid { MinHeight = 40, Margin = new(12, 0, 12, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new DockPanel(); header.Children.Add(heading);
        var kind = Categories.FirstOrDefault(c => c.Code == category);
        if (kind.Label is not null)
        {
            var pill = new Border { Width = kind.Pill, Height = 18, CornerRadius = new(9), BorderThickness = new(1), BorderBrush = Brush(kind.Color), Background = Brushes.White, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = kind.Label, FontSize = 12, Foreground = Brush(kind.Color), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            AutomationProperties.SetAutomationId(pill, "PublicNoticeCategory"); heading.Children.Add(pill);
        }
        heading.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var dateText = new TextBlock { Text = date, FontSize = 12, Foreground = Brush("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 8, 0) };
        Grid.SetColumn(dateText, 1); header.Children.Add(dateText);
        var icon = new SvgViewbox { Width = 16, Height = 16, Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 2); header.Children.Add(icon);
        var content = new Border { Background = Brushes.White, BorderBrush = Brush("#EAEAEA"), BorderThickness = new(1), Margin = new(12, 0, 12, 8), Padding = new(12, 8, 12, 8),
            Child = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 12 } };
        var card = new Expander { Header = header, Content = content, IsExpanded = expanded, Margin = new(0, 0, 0, 8), Template = NoticeTemplate() };
        AutomationProperties.SetName(card, title);
        void UpdateIcon() => icon.Source = new Uri($"pack://application:,,,/Semojum.Feature.Public.Wpf;component/Assets/Figma/public-notice-chevron-{(card.IsExpanded ? "down" : "right")}.svg");
        card.Expanded += (_, _) => UpdateIcon(); card.Collapsed += (_, _) => UpdateIcon(); UpdateIcon();
        return card;
    }

    private static ControlTemplate NoticeTemplate() => (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Expander">
          <Border x:Name="Card" Background="White" BorderBrush="#EAEAEA" BorderThickness="1">
            <StackPanel>
              <ToggleButton IsChecked="{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" Content="{TemplateBinding Header}" HorizontalContentAlignment="Stretch" BorderThickness="0" Background="Transparent" Padding="0">
                <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="{TemplateBinding Background}"><ContentPresenter HorizontalAlignment="Stretch" /></Border></ControlTemplate></ToggleButton.Template>
              </ToggleButton>
              <ContentPresenter x:Name="Details" Content="{TemplateBinding Content}" Visibility="Collapsed" />
            </StackPanel>
          </Border>
          <ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Card" Property="Background" Value="#EFF6FC"/><Setter TargetName="Card" Property="BorderBrush" Value="#005FB8"/><Setter TargetName="Details" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
}
// Same order as PublicInquiryDialog.Categories.
public enum PublicInquiryCategory { AccountIssue, BrailleResult, CreditPayment, Usage }
public sealed record InquiryMessage(string Category, string Email, string Organization, string Title, string Body, UploadFile[] Files);
public sealed class PublicInquiryDialog : CompactDialog
{
    // The Figma categories in order; the server takes each code as the inquiry type.
    private static readonly (string Label, string Type)[] Categories = [("계정 · 발급", "ACCOUNT"), ("점역 결과", "RESULT"), ("크레딧 · 결제", "BILLING"), ("사용법", "USAGE")];
    // 「최대 3개 · 10MB 제한」: images, documents and zip, as on the board.
    private const int MaxFiles = 3; private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".hwp", ".hwpx", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".brf", ".zip"];
    private sealed record Attachment(string Name, string Path) { public override string ToString() => Name; }
    private readonly ObservableCollection<Attachment> attachments = [];

    public bool Standalone { get; }
    public PublicInquiryDialog(SemojumApiClient api, string? initialTitle = null,
        PublicInquiryCategory category = PublicInquiryCategory.AccountIssue, bool standalone = false,
        string? initialEmail = null, Func<InquiryMessage, CancellationToken, Task>? sendMemberInquiry = null)
        : base("문의하기 - 누리닷", standalone ? 1440 : 820, standalone ? 1000 : 510, editor: true)
    {
        Standalone = standalone;
        var form = new PublicInquiryForm();
        var type = form.CategoryList;
        type.ItemsSource = Categories.Select(c => c.Label).ToArray();
        type.SelectedIndex = (int)category;
        var initialType = type.SelectedItem;
        var email = form.EmailInput;
        email.Text = initialEmail ?? "";
        var organization = form.OrganizationInput;
        var title = form.TitleInput;
        title.Text = initialTitle ?? "";
        var message = form.MessageInput;
        var attachment = form.AttachmentButton;
        var attached = form.AttachedFiles;
        var consent = form.Consent;
        if (sendMemberInquiry is not null)
        {
            const string memberConsent = "문의 답변을 위한 개인정보 수집 · 이용에 동의합니다.";
            ((TextBlock)consent.Content).Text = memberConsent;
            AutomationProperties.SetName(consent, memberConsent);
        }
        var actions = form.FormActions;

        var formContainer = new Border { Padding = new(32, 24, 32, 24), Child = form, VerticalAlignment = VerticalAlignment.Top };
        if (standalone)
        {
            Loaded += (_, _) => WindowState = WindowState.Maximized;
            var backText = new StackPanel { Orientation = Orientation.Horizontal };
            backText.Children.Add(DesignIcon("inquiry-back.svg", 16, 16, "Semojum.Feature.Public.Wpf"));
            backText.Children.Add(new TextBlock { Text = "돌아가기", Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var back = new Button { Content = backText, HorizontalAlignment = HorizontalAlignment.Left, Height = 32, Padding = new(8, 0, 8, 0),
                Background = Brushes.Transparent, BorderThickness = new(0) };
            AutomationProperties.SetName(back, "돌아가기");
            back.Click += (_, _) => Close();
            var header = new Border { Height = 48, Padding = new(16, 0, 16, 0), Background = Brushes.White,
                BorderBrush = Brush("#EAEAEA"), BorderThickness = new(0, 0, 0, 1), Child = back };
            DockPanel.SetDock(header, Dock.Top); Root.Children.Add(header);
            // The standalone page includes the frame boundary around its title bar and body.
            var framedContent = (UIElement)Content;
            Content = null;
            Content = new Border { Background = Brush("#F3F3F3"), BorderBrush = Brush("#D1D1D1"),
                BorderThickness = new(1), CornerRadius = new(8), Child = framedContent };
            form.Heading.Visibility = Visibility.Visible;
            form.Heading.FontSize = 28;
            form.Heading.Height = form.Heading.LineHeight = 36;
            form.Heading.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            var categoryStyle = new Style(typeof(ListBoxItem), type.ItemContainerStyle);
            categoryStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 20d));
            type.ItemContainerStyle = categoryStyle;
            attachment.BorderBrush = Brush("#D1D1D1");
            formContainer.MaxWidth = 720; formContainer.Margin = new(12, 32, 12, 24);
            formContainer.Background = Brushes.White; formContainer.CornerRadius = new(8);
            formContainer.BorderBrush = Brush("#EAEAEA"); formContainer.BorderThickness = new(1);
        }
        var scroll = new ScrollViewer { Content = formContainer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brush("#F3F3F3"), Focusable = false };
        AutomationProperties.SetAutomationId(scroll, "InquiryFormScroll"); Body(scroll);
        attachment.Click += (_, _) =>
        {
            var picker = new OpenFileDialog { Multiselect = true, Filter = "이미지·문서·zip|" + string.Join(';', Extensions.Select(e => "*" + e)) };
            if (picker.ShowDialog(this) != true) return;
            Status.Text = ""; try { Attach(picker.FileNames); } catch (Exception ex) when (ex is ArgumentException or IOException) { Status.Text = ex.Message; }
        };
        attached.ItemsSource = attachments;
        Semojum.Desktop.Controls.AttachmentPresentation.Configure<Attachment>(attached, file => attachments.Remove(file), "PublicInquiryRemoveAttachment");
        attachments.CollectionChanged += (_, _) => attached.Visibility = attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var attachedMenu = new ContextMenu(); var detach = new MenuItem { Header = "선택 첨부 제거" }; attachedMenu.Items.Add(detach); attached.ContextMenu = attachedMenu;
        detach.Click += (_, _) => { if (attached.SelectedItem is Attachment a) attachments.Remove(a); };
        attached.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Delete && attached.SelectedItem is Attachment a) { attachments.Remove(a); e.Handled = true; } };

        var submitted = false; var discardConfirmed = false;
        Closing += (_, e) =>
        {
            if (e.Cancel || submitted || discardConfirmed || Equals(type.SelectedItem, initialType) && email.Text.Trim() == (initialEmail ?? "").Trim()
                && string.IsNullOrWhiteSpace(organization.Text) && string.Equals(title.Text.Trim(), (initialTitle ?? "").Trim(), StringComparison.Ordinal) && string.IsNullOrWhiteSpace(message.Text)
                && attachments.Count == 0) return;
            var choice = NoticeDialog.Choose(this, "작성 중인 문의를 닫으시겠습니까?", "계속 작성하면 입력한 내용을 유지합니다.", ["계속 작성", "닫기"], width: 440);
            if (choice != 1) e.Cancel = true; else discardConfirmed = true;
        };
        async Task SubmitAsync()
        {
            if (consent.IsChecked != true) throw new ArgumentException("개인정보 수집·이용에 동의해 주세요.");
            if (!MailAddress.TryCreate(email.Text.Trim(), out var address) || address.Address != email.Text.Trim() || string.IsNullOrWhiteSpace(message.Text)) throw new ArgumentException("이메일 주소와 문의 내용을 확인해 주세요.");
            var subject = title.Text.Trim(); var orgName = organization.Text.Trim(); var body = message.Text.Trim();
            if (subject.Length > 200 || orgName.Length > 100 || body.Length > 2000) throw new ArgumentException("제목은 200자, 기관명은 100자, 문의 내용은 2,000자까지 입력할 수 있습니다.");
            if (attachments.Count > MaxFiles) throw new ArgumentException("첨부는 최대 3개까지 보낼 수 있습니다.");
            var files = new List<UploadFile>();
            foreach (var file in attachments) { Check(file.Name, new FileInfo(file.Path).Length); files.Add(new(file.Name, await File.ReadAllBytesAsync(file.Path, Lifetime.Token))); }
            // The app sends multipart so attachments can travel with the fields (homepage inquiries stay JSON).
            // BE ModelAttribute binds individual fields; deployed OpenAPI also advertises a JSON request part. Send identical values once in each representation, without retrying the inquiry.
            HttpContent Form()
            {
                var form = new MultipartFormDataContent();
                var fields = JsonSerializer.Serialize(new
                {
                    type = Categories[Math.Max(type.SelectedIndex, 0)].Type, email = address.Address,
                    title = subject.Length > 0 ? subject : null, orgName = orgName.Length > 0 ? orgName : null, message = body, website = ""
                }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                using (var payload = JsonDocument.Parse(fields))
                    foreach (var field in payload.RootElement.EnumerateObject())
                        form.Add(new StringContent(field.Value.GetString() ?? "", System.Text.Encoding.UTF8), field.Name);
                form.Add(new StringContent(fields, System.Text.Encoding.UTF8, "application/json"), "request");
                foreach (var file in files) { var part = new ByteArrayContent(file.Bytes); part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); form.Add(part, "files", file.FileName); }
                return form;
            }
            try
            {
                if (sendMemberInquiry is null) await api.SubmitInquiryAsync((Func<HttpContent>)Form, Lifetime.Token);
                else await sendMemberInquiry(new(Categories[Math.Max(type.SelectedIndex, 0)].Type, address.Address, orgName, subject, body, files.ToArray()), Lifetime.Token);
            }
            catch (SemojumApiException ex) when (ex.Code == "INQUIRY4001") { throw new InvalidOperationException("첨부는 이미지·문서·zip 파일로 최대 3개까지 보낼 수 있습니다.", ex); }
            catch (SemojumApiException ex) when (ex.Code == "INQUIRY4130" || ex.Status == HttpStatusCode.RequestEntityTooLarge) { throw new InvalidOperationException("첨부 파일은 한 개에 10MB까지 보낼 수 있습니다.", ex); }
            submitted = true;
            NoticeDialog.Choose(this, "문의를 접수했습니다.", sendMemberInquiry is null ? "적어 주신 메일 주소로 회신해드립니다." : "마이페이지의 문의 내역에서 답변을 확인할 수 있습니다.", ["확인"], width: 440); DialogResult = true;
        }
        if (!standalone)
        {
            var cancel = Button("취소", () => { Close(); return Task.CompletedTask; });
            cancel.IsCancel = false; PublicDialogDesign.StyleAction(cancel);
            cancel.Margin = new(0, 0, 8, 0); actions.Children.Add(cancel);
        }
        var send = Button("보내기", () => RunAsync(SubmitAsync), true); PublicDialogDesign.StyleAction(send, true); send.Margin = new Thickness(0); actions.Children.Add(send);
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
    }
    /// <summary>Adds chosen files after the same checks as sending; a rejected choice adds none of its files.</summary>
    internal void Attach(IEnumerable<string> paths)
    {
        var chosen = paths.ToArray();
        if (attachments.Count + chosen.Length > MaxFiles) throw new ArgumentException("첨부는 최대 3개까지 보낼 수 있습니다.");
        foreach (var path in chosen) Check(System.IO.Path.GetFileName(path), new FileInfo(path).Length);
        foreach (var path in chosen) attachments.Add(new(System.IO.Path.GetFileName(path), path));
    }
    private static void Check(string name, long size)
    {
        if (!Extensions.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant())) throw new ArgumentException("이미지·문서·zip 파일만 첨부할 수 있습니다: " + name);
        if (size > MaxBytes) throw new ArgumentException("파일 한 개는 10MB까지 첨부할 수 있습니다: " + name);
    }
}
