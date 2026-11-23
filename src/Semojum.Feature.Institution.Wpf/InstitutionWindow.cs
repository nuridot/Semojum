using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Semojum.Core.Integration;
using Semojum.Desktop.Services;

namespace Semojum.Desktop.Dialogs;

/// <summary>Institution administrator's top-level desktop window 「기관 관리 - 누리닷」; administrators do not use the editor.</summary>
public sealed partial class InstitutionWindow : CompactDialog
{
    private Window ModalOwner => Window.GetWindow(Root) ?? this;
    private readonly SemojumApiClient api;
    private readonly Guid? institutionSessionMarker;
    private readonly Func<Task<bool>> logout;
    private bool loggingOut;
    public bool LogoutRequested { get; private set; }
    internal TextBlock Identity { get; } = new() { FontSize = 14, Foreground = FigmaDialogElements.Brush("#5F5F5F") };
    internal TextBlock Allocated { get; } = new() { Text = "—", FontSize = 20, LineHeight = 28, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.SemiBold };
    internal TextBlock Balance { get; } = new() { Text = "—", FontSize = 20, LineHeight = 28, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.SemiBold };
    internal TextBlock Contract { get; } = new() { Text = "—", FontSize = 20, LineHeight = 28, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.SemiBold };
    internal TextBlock ContractRemaining { get; } = new() { Text = "정보 없음", Foreground = FigmaDialogElements.Brush("#5F5F5F"), FontSize = 14, FontWeight = FontWeights.SemiBold };
    internal TextBlock CreditDetail { get; } = new() { Text = "사용량 정보 없음", Foreground = FigmaDialogElements.Brush("#5F5F5F") };
    internal TextBlock UsagePercent { get; } = new() { Text = "정보 없음", Foreground = FigmaDialogElements.Brush("#5F5F5F") };
    internal TextBlock ErrorText { get; } = new() { Foreground = FigmaDialogElements.Brush("#C42B1C"), TextWrapping = TextWrapping.Wrap };
    internal ProgressBar UsageBar { get; } = new() { Minimum = 0, Maximum = 100, Height = 8, Template = UsageBarTemplate() };
    internal DataGrid Accounts { get; }
    internal DataGrid Orders { get; }
    internal DataGrid Notices { get; }
    internal TextBlock PendingRequests { get; } = new() { Foreground = FigmaDialogElements.Brush("#5F5F5F"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    internal TextBox ReceiptAddress { get; } = new() { Width = 200, Height = 28, MinHeight = 0, Padding = new(11, 0, 11, 0), MaxLength = 254, IsEnabled = false, Background = Brushes.White, BorderBrush = FigmaDialogElements.Brush("#D1D1D1"), HorizontalAlignment = HorizontalAlignment.Left };
    internal TextBlock ReceiptHint { get; } = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Text = "이메일 주소 형식을 확인해 주세요. 예: account@kblib.or.kr", Foreground = FigmaDialogElements.Brush("#C42B1C"), Margin = new(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    internal StackPanel Chart { get; } = new() { Orientation = Orientation.Horizontal };
    internal TextBlock ChartEmpty { get; } = new() { Text = "월별 사용량 정보 없음", Foreground = FigmaDialogElements.Brush("#5F5F5F"), TextWrapping = TextWrapping.Wrap };
    internal TextBlock[] ChartTicks { get; } = Enumerable.Range(0, 6).Select(_ => new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Right }).ToArray();
    internal TextBlock ChartAverage { get; } = new() { Text = "6개월 평균 정보 없음", Foreground = FigmaDialogElements.Brush("#5F5F5F"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Right };
    internal ScrollViewer ChartScroll { get; } = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    internal Grid ChartArea { get; } = new();
    private Border? chartCard;
    private bool chartHighContrast;
    internal Button AccountRequest { get; }
    internal Button CreditRequest { get; }
    internal Button LogoutItem { get; }
    internal MenuItem? MyPageItem { get; }

    public InstitutionWindow(SemojumApiClient api, Func<Task<bool>> logout, Action<InstitutionWindow>? openMyPage = null, Action<Window>? openInquiry = null) : base("기관 관리 - 누리닷", 1440, 1000, editor: true)
    {
        this.api = api; this.logout = logout; institutionSessionMarker = api.SessionMarker;
        MinWidth = 480; MinHeight = 360; Root.Background = FigmaDialogElements.Brush("#F3F3F3"); ApplyCaption();
        var content = new StackPanel { Margin = new(24, 0, 24, 24) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = content };
        Body(scroll);
        var identityButton = DashboardButton("", 0);
        identityButton.Content = Identity; identityButton.Padding = new(0); identityButton.Background = identityButton.BorderBrush = Brushes.Transparent;
        AutomationProperties.SetName(identityButton, "계정 메뉴"); identityButton.ToolTip = "계정 메뉴";
        var accountMenu = new ContextMenu(); identityButton.ContextMenu = accountMenu;
        identityButton.Click += (_, _) => { accountMenu.PlacementTarget = identityButton; accountMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; accountMenu.IsOpen = true; };
        if (openMyPage is not null)
        {
            MyPageItem = new MenuItem { Header = "마이페이지 · 사용량" }; MyPageItem.Click += (_, _) => openMyPage(this); accountMenu.Items.Add(MyPageItem);
        }
        var billingItem = new MenuItem { Header = "계약·청구 문서" }; billingItem.Click += (_, _) =>
        {
            if (api.SessionMarker == institutionSessionMarker && api.Account?.Role == "ROLE_ORG_ADMIN")
                new InstitutionBillingDialog(Client, () => api.SessionMarker == institutionSessionMarker && api.Account?.Role == "ROLE_ORG_ADMIN", url => ReceiptLauncher(url)) { Owner = ModalOwner }.ShowDialog();
        }; accountMenu.Items.Add(billingItem);
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "기관 관리", FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0) }); title.Children.Add(identityButton);
        var commands = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var inquiry = DashboardButton("문의하기", 88); inquiry.IsEnabled = openInquiry is not null; inquiry.Click += (_, _) => openInquiry?.Invoke(ModalOwner);
        var help = DashboardButton("도움말", 88); help.Click += (_, _) => CommonWindowServices.ShowHelp(ModalOwner, "사용량과 작업 이력");
        var refresh = DashboardButton("새로 고침 (F5)", 104); refresh.Click += async (_, _) => await RefreshAsync();
        LogoutItem = DashboardButton("로그아웃", 90); LogoutItem.Foreground = FigmaDialogElements.Brush("#C42B1C"); LogoutItem.Background = LogoutItem.BorderBrush = Brushes.Transparent;
        var signout = new StackPanel { Orientation = Orientation.Horizontal }; var signoutIcon = FigmaDialogElements.DesignIcon("institution-logout.svg", 16, 16, "Semojum.Feature.Institution.Wpf"); signoutIcon.Margin = new(0,0,8,0); signout.Children.Add(signoutIcon); signout.Children.Add(new TextBlock { Text = "로그아웃" }); LogoutItem.Content = signout;
        AutomationProperties.SetName(LogoutItem, "로그아웃"); LogoutItem.Click += async (_, _) => await LogoutAsync();
        foreach (var command in new[] { inquiry, help, refresh, LogoutItem }) { command.Margin = new(8, 0, 0, 0); commands.Children.Add(command); }
        var heading = new Grid { Margin = new(0, 12, 0, 12) }; heading.RowDefinitions.Add(new() { Height = GridLength.Auto }); heading.RowDefinitions.Add(new() { Height = GridLength.Auto });
        heading.Children.Add(title); heading.Children.Add(commands); content.Children.Add(heading);
        heading.SizeChanged += (_, e) => Grid.SetRow(commands, e.NewSize.Width < title.DesiredSize.Width + 408 ? 1 : 0);
        Root.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F5) { e.Handled = true; await RefreshAsync(); }
            else if (e.Key == System.Windows.Input.Key.F1 && !ReferenceEquals(ModalOwner, this)) { e.Handled = true; CommonWindowServices.ShowHelp(ModalOwner, "사용량과 작업 이력"); }
        };
        content.Children.Add(ErrorText); ErrorText.SetBinding(VisibilityProperty, new Binding("Text") { Source = ErrorText, Converter = new InstitutionReceiptVisibilityConverter() });
        CreditRequest = DashboardButton("＋ 크레딧 추가 요청", 125, primary: true); CreditRequest.IsEnabled = false; CreditRequest.Click += async (_, _) => await RequestCreditAsync();
        AccountRequest = DashboardButton("＋ 계정 발급 요청", 117); AccountRequest.IsEnabled = false; AccountRequest.Click += async (_, _) => await RequestAccountAsync();
        var allocated = DashboardCard("할당 크레딧", null, true, Allocated, UsageBar, UsagePercent);
        CreditDetail.ToolTip = "최근 6개월 계약 크레딧의 실제 차감액과 처리 쪽수 기준";
        var balance = DashboardCard("잔여 크레딧", CreditRequest, true, Balance, CreditDetail);
        var contract = DashboardCard("계약", null, true, Contract, ContractRemaining);
        content.Children.Add(CardRow(allocated, balance, contract));
        chartCard = MakeMonthlyCard();
        content.Children.Add(CardRow(chartCard, DashboardCard("유형별 소비 비중", null, false, LayoutStats), DashboardCard("계정별 사용량 · 이번 달", null, false, AccountStatsScroll)));
        ApplyChartContrast(SystemParameters.HighContrast);
        System.ComponentModel.PropertyChangedEventHandler contrastChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.BeginInvoke(new Action(() => ApplyChartContrast(SystemParameters.HighContrast)));
        };
        SystemParameters.StaticPropertyChanged += contrastChanged; Closed += (_, _) => SystemParameters.StaticPropertyChanged -= contrastChanged;
        Notices = Table(("받은 날", "CreatedAt"), ("제목", "Title")); Notices.Columns.Insert(1, StatusColumn("분류", "Scope", "notice"));
        OpenOnClick<InstitutionNotice>(Notices, OpenNoticeAsync);
        var noticeCard = DashboardCard("공지", null, false, Notices);
        Accounts = Table(("계정 ID", "LoginId"), ("별칭", "Alias"), ("마지막 로그인", "LastLoginAt"), ("한도", "LimitLabel"), ("이달 사용", "UsedCredits")); Accounts.Columns.Insert(2, StatusColumn("상태", "Status", "account"));
        ((DataGridTextColumn)Accounts.Columns[3]).Binding = new Binding("LastLoginAt") { Converter = new InstitutionLastSeenConverter() };
        ((DataGridTextColumn)Accounts.Columns[5]).Binding = new Binding("UsedCredits") { StringFormat = "N0", TargetNullValue = "—" };
        var loginId = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle); loginId.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14d)); loginId.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        loginId.Setters.Add(new Setter(TextBlock.ForegroundProperty, FigmaDialogElements.Brush("#003E92")));
        var requestLine = new DataTrigger { Binding = new Binding(nameof(InstitutionAccountRow.Account)), Value = null }; requestLine.Setters.Add(new Setter(TextBlock.ForegroundProperty, FigmaDialogElements.Brush("#5F5F5F"))); loginId.Triggers.Add(requestLine);
        ((DataGridTextColumn)Accounts.Columns[0]).ElementStyle = loginId;
        var accountAction = new FrameworkElementFactory(typeof(Button)); accountAction.SetBinding(ContentProperty, new Binding("Control")); accountAction.SetValue(HeightProperty, 20d); accountAction.SetValue(MinHeightProperty, 0d); accountAction.SetValue(MinWidthProperty, 0d);
        accountAction.SetValue(Control.BackgroundProperty, Brushes.Transparent); accountAction.SetValue(Control.BorderBrushProperty, Brushes.Transparent); accountAction.SetValue(Control.ForegroundProperty, FigmaDialogElements.Brush("#003E92")); accountAction.SetValue(Control.TemplateProperty, FigmaDialogElements.SurfaceTemplate(0));
        accountAction.SetValue(PaddingProperty, new Thickness(0)); accountAction.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        accountAction.SetBinding(VisibilityProperty, new Binding("Control") { Converter = new InstitutionReceiptVisibilityConverter() });
        accountAction.SetBinding(AutomationProperties.NameProperty, new MultiBinding { StringFormat = "{0} {1}", Bindings = { new Binding("LoginId"), new Binding("Control") } });
        accountAction.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(async (sender, e) => { e.Handled = true; if (sender is FrameworkElement { DataContext: InstitutionAccountRow row }) await RunRowControlAsync(row); }));
        Accounts.Columns.Add(new DataGridTemplateColumn { Header = "", Width = 128, CanUserSort = false, CellTemplate = new DataTemplate { VisualTree = accountAction } });
        OpenOnClick<InstitutionAccountRow>(Accounts, row => row.Account is { } account ? OpenAccountAsync(account) : Task.CompletedTask);
        Accounts.ColumnHeaderStyle = (Style)Application.Current.FindResource("InstitutionAccountHeader");
        var widths = new[] { 188, 168, 128, 148, 128, 128 }; for (int i = 0; i < widths.Length; i++) Accounts.Columns[i].Width = widths[i];
        Accounts.Columns[2].SortMemberPath = nameof(InstitutionAccountRow.Status); Accounts.Columns[4].SortMemberPath = "Account.CreditLimit";
        var accountTools = new StackPanel { Orientation = Orientation.Horizontal };
        PendingRequests.Margin = new(0, 0, 8, 0); accountTools.Children.Add(PendingRequests); accountTools.Children.Add(AccountRequest);
        var accountsCard = DashboardCard("소속 계정", accountTools, false, Accounts); accountsCard.Margin = new(0, 0, 0, 16); content.Children.Add(accountsCard);
        Orders = Table(("일자", "OrderDate"), ("내용", "Description"), ("금액", "AmountKrw"), ("계산서", "InvoiceStatus")); Orders.Columns.Insert(3, StatusColumn("결제", "PaidAt", "payment"));
        ((DataGridTextColumn)Orders.Columns[^1]).Binding = new Binding("InvoiceStatus") { Converter = new InstitutionStatusTextConverter(), ConverterParameter = "invoice" };
        var receiptCell = new FrameworkElementFactory(typeof(Grid));
        var receiptLink = new FrameworkElementFactory(typeof(Button)); var download = new FrameworkElementFactory(typeof(SharpVectors.Converters.SvgViewbox)); download.SetValue(WidthProperty, 16d); download.SetValue(HeightProperty, 16d); download.SetValue(SharpVectors.Converters.SvgViewbox.SourceProperty, new Uri("pack://application:,,,/Semojum.Feature.Institution.Wpf;component/Assets/Figma/institution-download.svg")); receiptLink.SetValue(ContentTemplateProperty, new DataTemplate { VisualTree = download }); receiptLink.SetValue(ContentProperty, "내려받기"); receiptLink.SetValue(HeightProperty, 28d); receiptLink.SetValue(MinHeightProperty, 0d); receiptLink.SetValue(MinWidthProperty, 0d);
        receiptLink.SetValue(PaddingProperty, new Thickness(2, 0, 2, 0)); receiptLink.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        receiptLink.SetValue(Control.ForegroundProperty, FigmaDialogElements.Brush("#003E92")); receiptLink.SetValue(Control.BackgroundProperty, Brushes.Transparent); receiptLink.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        receiptLink.SetValue(Control.TemplateProperty, FigmaDialogElements.SurfaceTemplate(0));
        receiptLink.SetBinding(VisibilityProperty, new Binding("ReceiptFileName") { Converter = new InstitutionReceiptVisibilityConverter() });
        receiptLink.SetBinding(ToolTipProperty, new Binding("ReceiptFileName"));
        receiptLink.SetBinding(AutomationProperties.NameProperty, new Binding("Description") { StringFormat = "주문 {0} 증빙 내려받기" });
        receiptLink.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(async (sender, e) => { e.Handled = true; if (sender is FrameworkElement { DataContext: InstitutionOrder order }) await OpenReceiptForAsync(order); }));
        receiptCell.AppendChild(receiptLink);
        var noReceipt = new FrameworkElementFactory(typeof(TextBlock)); noReceipt.SetValue(TextBlock.TextProperty, "—"); noReceipt.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        noReceipt.SetBinding(VisibilityProperty, new Binding("ReceiptFileName") { Converter = new InstitutionReceiptVisibilityConverter(), ConverterParameter = "missing" }); receiptCell.AppendChild(noReceipt);
        Orders.Columns.Add(new DataGridTemplateColumn { Header = "증빙", Width = 42, CanUserSort = false, CellTemplate = new DataTemplate { VisualTree = receiptCell } });
        OpenOnClick<InstitutionOrder>(Orders, OpenOrderAsync);
        var receiptLine = new Grid(); receiptLine.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); receiptLine.ColumnDefinitions.Add(new());
        receiptLine.RowDefinitions.Add(new() { Height = GridLength.Auto }); receiptLine.RowDefinitions.Add(new() { Height = GridLength.Auto });
        receiptLine.Children.Add(new TextBlock { Text = "증빙 받는 메일", Foreground = FigmaDialogElements.Brush("#5F5F5F"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) });
        AutomationProperties.SetName(ReceiptAddress, "증빙 받는 메일"); Grid.SetColumn(ReceiptAddress, 1); receiptLine.Children.Add(ReceiptAddress);
        Grid.SetColumn(ReceiptHint, 1); Grid.SetRow(ReceiptHint, 1); receiptLine.Children.Add(ReceiptHint);
        ReceiptAddress.TextChanged += (_, _) => ReceiptHint.Visibility = Visibility.Collapsed;
        ReceiptAddress.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; ReceiptAddress.Text = receiptEmail ?? ""; }
            else if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await SaveReceiptEmailAsync(); }
        };
        ReceiptAddress.LostKeyboardFocus += async (_, _) => await SaveReceiptEmailAsync();
        var accountRows = new Style(typeof(DataGridRow)); accountRows.Setters.Add(new Setter(MarginProperty, new Thickness(0,8,0,0))); Accounts.RowStyle = accountRows;
        var listRows = new Style(typeof(DataGridRow)); var spacedRow = new DataTrigger { Binding = new Binding("(ItemsControl.AlternationIndex)") { RelativeSource = RelativeSource.Self }, Value = 0 };
        listRows.Setters.Add(new Setter(MarginProperty, new Thickness(0,8,0,0))); spacedRow.Setters.Add(new Setter(MarginProperty, new Thickness(0))); listRows.Triggers.Add(spacedRow);
        foreach (var list in new[] { Notices, Orders }) { list.RowStyle = listRows; list.AlternationCount = int.MaxValue; list.HeadersVisibility = DataGridHeadersVisibility.None; list.RowHeight = 36; list.MinHeight = 36; }
        Notices.Columns[0].Width = 60; Notices.Columns[1].Width = 78;
        Orders.Columns[0].Width = 60; Orders.Columns[2].Width = 88; Orders.Columns[3].Width = 65; Orders.Columns[4].Width = 76;
        ((DataGridTextColumn)Orders.Columns[2]).Binding = new Binding("AmountKrw") { StringFormat = "{0:N0}원" };
        ((DataGridTextColumn)Orders.Columns[0]).Binding = new Binding("OrderDate") { Converter = new InstitutionShortDateConverter() };
        ((DataGridTextColumn)Notices.Columns[0]).Binding = new Binding("CreatedAt") { Converter = new InstitutionShortDateConverter() };
        foreach (var link in new[] { Notices.Columns[2], Orders.Columns[1] }.OfType<DataGridTextColumn>())
        {
            var style = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle); style.Setters.Add(new Setter(TextBlock.ForegroundProperty, FigmaDialogElements.Brush("#003E92"))); link.ElementStyle = style;
        }
        content.Children.Add(CardRow(noticeCard, DashboardCard("주문 내역", receiptLine, false, Orders)));
        Root.Loaded += async (_, _) => await RefreshAsync();
    }

    /// <summary>A row opens with one click or Enter; its own buttons (내려받기, 잠금 …) keep their action instead.</summary>
    private static void OpenOnClick<T>(DataGrid table, Func<T, Task> open) where T : class
    {
        bool InButton(object source)
        {
            for (var node = source as DependencyObject; node is not null && node != table; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
                if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
            return false;
        }
        table.PreviewMouseLeftButtonUp += async (_, e) =>
        {
            if (!InButton(e.OriginalSource) && ItemsControl.ContainerFromElement(table, e.OriginalSource as DependencyObject) is DataGridRow { Item: T item }) await open(item);
        };
        table.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter || InButton(e.OriginalSource) || table.SelectedItem is not T item) return;
            e.Handled = true; await open(item);
        };
    }

    internal async Task LogoutAsync()
    {
        if (loggingOut) return;
        loggingOut = true; LogoutItem.IsEnabled = false;
        try { if (await logout()) { LogoutRequested = true; Close(); } }
        catch (Exception ex) { ErrorText.Text = "로그아웃하지 못했습니다. " + ex.Message; }
        finally { loggingOut = false; LogoutItem.IsEnabled = true; }
    }
    private void ApplyCaption()
    {
        if (Content is not Grid frame || frame.Children[0] is not Border { Child: DockPanel heading }) return;
        if (heading.Children.OfType<StackPanel>().FirstOrDefault() is { } buttons)
        {
            buttons.Margin = new(0);
            foreach (var (button, name) in buttons.Children.OfType<Button>().Zip(new[] { "min", "max", "close" }))
            { button.Width = 46; button.Height = 32; button.Content = FigmaDialogElements.DesignIcon($"institution-caption-{name}.svg", 16, 16, "Semojum.Feature.Institution.Wpf"); }
        }
    }
    private static DataGridTemplateColumn StatusColumn(string title, string property, string kind)
    {
        var pill = new FrameworkElementFactory(typeof(Border)); pill.SetValue(Border.BackgroundProperty, Brushes.White); pill.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        pill.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); pill.SetValue(HeightProperty, 20d); pill.SetValue(MinWidthProperty, 40d);
        pill.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left); pill.SetValue(Border.PaddingProperty, new Thickness(8, 0, 8, 0));
        pill.SetBinding(Border.BorderBrushProperty, new Binding(property) { Converter = new InstitutionStatusBrushConverter(), ConverterParameter = kind });
        var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); text.SetValue(FontSizeProperty, 12d);
        text.SetBinding(TextBlock.TextProperty, new Binding(property) { Converter = new InstitutionStatusTextConverter(), ConverterParameter = kind });
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(property) { Converter = new InstitutionStatusBrushConverter(), ConverterParameter = kind });
        pill.AppendChild(text); return new DataGridTemplateColumn { Header = title, Width = new DataGridLength(1, DataGridLengthUnitType.Star), CellTemplate = new DataTemplate { VisualTree = pill } };
    }
    private static DataGrid Table(params (string Label, string Path)[] columns)
    {
        var table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new(0), Background = Brushes.White,
            MinHeight = 32, MaxHeight = 330, RowHeight = 40, Style = (Style)Application.Current.FindResource("InstitutionListGrid"), HorizontalGridLinesBrush = FigmaDialogElements.Brush("#EAEAEA") };
        foreach (var (label, path) in columns) table.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(path) { TargetNullValue = "—" }, Width = new(1, DataGridLengthUnitType.Star) });
        AutomationProperties.SetName(table, string.Join(" · ", columns.Select(c => c.Label))); return table;
    }
    private static ControlTemplate UsageBarTemplate()
    {
        var template = new ControlTemplate(typeof(ProgressBar));
        var track = new FrameworkElementFactory(typeof(Border)); track.Name = "InstitutionTrack"; track.SetValue(Border.BackgroundProperty, FigmaDialogElements.Brush("#E3E6EB")); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var fill = new FrameworkElementFactory(typeof(Border)); fill.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); fill.SetValue(Border.BackgroundProperty, FigmaDialogElements.Brush("#005FB8")); fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var width = new MultiBinding { Converter = new Controls.ProgressWidthConverter() };
        width.Bindings.Add(new Binding("ActualWidth") { ElementName = "InstitutionTrack" });
        foreach (var property in new[] { "Value", "Minimum", "Maximum" }) width.Bindings.Add(new Binding(property) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        fill.SetBinding(WidthProperty, width); track.AppendChild(fill); template.VisualTree = track; return template;
    }
}
