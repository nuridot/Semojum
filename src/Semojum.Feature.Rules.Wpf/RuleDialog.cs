using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Desktop.Dialogs;

public sealed class RuleDialog : CompactDialog
{
    private const int SearchPageSize = 4;
    internal StackPanel RuleResults { get; } = new();
    public Action<Window>? OpenAppliedRules { get; init; }
    private readonly RuleClient client;
    private readonly RuleEvidence[] evidence;
    private readonly TextBox query = new() { Height = 36, MaxLength = 100, FontSize = 14, Padding = new(40, 0, 8, 0),
        Background = Brushes.White, BorderBrush = FigmaDialogElements.Brush("#D1D1D1"), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TreeView contents = new() { BorderThickness = new(0), Padding = new(4, 8, 4, 8), Background = Brushes.Transparent };
    private readonly TextBlock count = new() { FontSize = 14, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock empty = new() { Text = "검색 결과가 없습니다.", FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly DockPanel pager = new() { Height = 32, Margin = new(0, 12, 0, 0) };
    private readonly TextBox pageInput = new() { Width = 96, Height = 32, Background = Brushes.White, BorderBrush = FigmaDialogElements.Brush("#D1D1D1"),
        VerticalContentAlignment = VerticalAlignment.Center, Padding = new(12, 0, 4, 0) };
    private readonly TextBlock pageTotal = new() { Margin = new(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = FigmaDialogElements.Brush("#5F5F5F") };
    private readonly WrapPanel filters = new();
    private readonly Dictionary<string, RuleContentsNode> articles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> breadcrumbs = new(StringComparer.Ordinal);
    private readonly ScrollViewer reader = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly string[] sources = ["전체", "한국 점자 규정", "점자 도서 제작 지침", "점자 자료 제작 지침"];
    private static readonly string[] Codes = ["", "MCST", "NLD", "NISE"];
    private IReadOnlyList<RuleContentsNode> documents = [];
    private int filterIndex, page, totalPages;
    private string[]? rankedMatches;
    private readonly Dictionary<string, TreeViewItem> articleItems = new(StringComparer.Ordinal);
    private string submitted = "";
    private Button previous = null!, next = null!;
    private readonly Style branchStyle, cardStyle;
    private bool updatingTree;

    public RuleDialog(SemojumApiClient api, RuleEvidence[] evidence, string selectedSource, string selectedBraille,
        string? initialQuery = null) : base("점자 규정 찾아보기 - 누리닷", 1100, 760, editor: true)
    {
        this.evidence = evidence;
        client = new RuleClient(api);
        var frame = (UIElement)Content;
        Content = null;
        Content = new Border { BorderThickness = new(1), BorderBrush = FigmaDialogElements.Brush("#D1D1D1"), CornerRadius = new(8), Child = frame };
        query.Text = submitted = initialQuery ?? "";
        filterIndex = string.IsNullOrWhiteSpace(initialQuery) ? 1 : 0;
        var styles = new ResourceDictionary { Source = new Uri("/Semojum.Feature.Rules.Wpf;component/Styles/RuleContents.xaml", UriKind.Relative) };
        branchStyle = (Style)styles["RuleBranch"];
        contents.Template = (ControlTemplate)styles["RuleTree"];
        reader.Template = (ControlTemplate)styles["RuleScroll"];
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/Semojum.UI.Common;component/Controls/FormEntry.xaml", UriKind.Relative) });
        pageInput.Template = query.Template = (ControlTemplate)FindResource("LayoutEntry");
        cardStyle = (Style)FindResource("RuleCard");
        var layout = new DockPanel { Background = Brushes.White };
        Body(layout);
        var filtersHost = SearchTools();
        DockPanel.SetDock(filtersHost, Dock.Top); layout.Children.Add(filtersHost);
        var footer = Footer(); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var columns = new Grid();
        var leftWidth = new ColumnDefinition { Width = new(340) };
        columns.ColumnDefinitions.Add(leftWidth); columns.ColumnDefinitions.Add(new());
        columns.SizeChanged += (_, _) => leftWidth.Width = new(Math.Min(340, Math.Max(120, columns.ActualWidth * .42)));
        var left = new Border { Background = FigmaDialogElements.Brush("#F9F9F9"), BorderThickness = new(0, 0, 1, 0), BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), Child = contents };
        AutomationProperties.SetName(contents, "규정 목차");
        ScrollViewer.SetHorizontalScrollBarVisibility(contents, ScrollBarVisibility.Disabled);
        columns.Children.Add(left);
        var right = new DockPanel { Margin = new(24, 16, 24, 16) };
        Grid.SetColumn(right, 1); columns.Children.Add(right); layout.Children.Add(columns);
        if (initialQuery is null && (selectedSource.Length > 0 || selectedBraille.Length > 0))
        {
            var excerpt = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8), MaxHeight = 64 };
            excerpt.Inlines.Add(new System.Windows.Documents.Run($"묵자: {selectedSource}\n점자: "));
            excerpt.Inlines.Add(new System.Windows.Documents.Run(selectedBraille) { FontFamily = (FontFamily)FindResource("BrailleFont") });
            DockPanel.SetDock(excerpt, Dock.Top); right.Children.Add(excerpt);
        }
        count.Foreground = empty.Foreground = FigmaDialogElements.Brush("#5F5F5F");
        count.Margin = new(0, 0, 0, 12); DockPanel.SetDock(count, Dock.Top); right.Children.Add(count);
        PopulatePager(); DockPanel.SetDock(pager, Dock.Bottom); right.Children.Add(pager);
        reader.Content = RuleResults;
        var content = new Grid(); content.Children.Add(reader); content.Children.Add(empty); right.Children.Add(content);
        var context = new ContextMenu(); var applied = new MenuItem { Header = "적용 규정" };
        applied.Click += (_, _) => OpenEvidence(); context.Items.Add(applied); layout.ContextMenu = context;
        contents.SelectedItemChanged += async (_, e) =>
        {
            if (updatingTree || e.NewValue is not TreeViewItem { Tag: RuleContentsNode { RuleId: { } id } node }) return;
            if (submitted.Length > 0 && ExpandResult(id)) return;
            var rank = rankedMatches is null ? -1 : Array.IndexOf(rankedMatches, id);
            if (submitted.Length > 0 && rank >= 0) await RunAsync(() => SearchAsync(rank / SearchPageSize, id));
            else ShowArticle(node);
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        ClearReader();
        Loaded += async (_, _) =>
        {
            await RunAsync(async () =>
            {
                documents = await client.ContentsAsync(Lifetime.Token);
                foreach (var doc in documents) Index(doc, "");
                if (submitted.Length > 0) await SearchAsync();
                else { BuildTree(); if (evidence.Length > 0) ShowEvidence(); }
            });
            if (IsVisible && IsActive) query.Focus();
        };
    }

    private FrameworkElement SearchTools()
    {
        var fields = new StackPanel { Margin = new(16, 12, 16, 12) };
        var host = new Grid(); host.Children.Add(query);
        AutomationProperties.SetName(query, "규정 검색어");
        var hint = new TextBlock { Text = "조항 번호 · 낱말 · 점형", FontSize = 14, Margin = new(40, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
            Foreground = FigmaDialogElements.Brush("#8A8A8A"), IsHitTestVisible = false };
        AutomationProperties.SetAutomationId(hint, "RuleSearchPlaceholder"); host.Children.Add(hint);
        var clear = Button("검색어 지우기", async () => { await RunAsync(async () => { query.Clear(); await Submit(); }); query.Focus(); });
        clear.Width = clear.Height = 24; clear.MinWidth = clear.MinHeight = 0; clear.Padding = new(0); clear.Margin = new(8, 0, 0, 0);
        clear.HorizontalAlignment = HorizontalAlignment.Left; clear.Background = clear.BorderBrush = Brushes.Transparent; clear.Template = FigmaDialogElements.SurfaceTemplate(4);
        AutomationProperties.SetName(clear, "검색어 지우기"); AutomationProperties.SetAutomationId(clear, "RuleSearchClear");
        var find = FigmaDialogElements.DesignIcon("catalog-find.svg", 16, 16, "Semojum.Feature.Rules.Wpf");
        find.HorizontalAlignment = HorizontalAlignment.Left; find.Margin = new(12, 0, 0, 0); find.IsHitTestVisible = false;
        clear.Content = FigmaDialogElements.DesignIcon("catalog-clear.svg", 16, 16, "Semojum.Feature.Rules.Wpf");
        host.Children.Add(find); host.Children.Add(clear);
        void QueryChanged()
        {
            var blank = query.Text.Length == 0;
            query.BorderBrush = FigmaDialogElements.Brush(blank ? "#D1D1D1" : "#005FB8");
            hint.Visibility = find.Visibility = blank ? Visibility.Visible : Visibility.Collapsed;
            clear.Visibility = blank ? Visibility.Collapsed : Visibility.Visible;
        }
        query.TextChanged += (_, _) => QueryChanged(); QueryChanged();
        query.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await RunAsync(Submit); query.Focus(); } };
        fields.Children.Add(host); filters.Margin = new(0, 12, 0, 0); fields.Children.Add(filters);
        var widths = new[] { 47, 103, 127, 127 };
        for (var i = 0; i < sources.Length; i++)
        {
            var index = i;
            var button = Button(i == 0 ? sources[i] : $"「{sources[i]}」", () => RunAsync(async () =>
            {
                filterIndex = index; PaintFilters(); await Submit();
                if (submitted.Length == 0 && index > 0)
                {
                    var document = contents.Items.Cast<TreeViewItem>().FirstOrDefault(item => ((RuleContentsNode)item.Tag).Title == sources[index]);
                    if (document != null) { document.IsExpanded = true; document.BringIntoView(); }
                }
            }));
            button.MinWidth = 0; button.Width = widths[i]; button.MinHeight = button.Height = 28; button.Padding = new(0); button.Margin = new(0, 0, 4, 0);
            button.Template = FigmaDialogElements.SurfaceTemplate(14); AutomationProperties.SetName(button, sources[i]); filters.Children.Add(button);
        }
        PaintFilters();
        return new Border { Background = FigmaDialogElements.Brush("#F3F3F3"), BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), BorderThickness = new(0, 0, 0, 1), Child = fields };
    }
    private void PaintFilters()
    {
        for (var i = 0; i < filters.Children.Count; i++)
        {
            var b = (Button)filters.Children[i]; var active = i == filterIndex;
            b.Background = FigmaDialogElements.Brush(active ? "#EFF6FC" : "#F3F3F3");
            b.BorderBrush = FigmaDialogElements.Brush(active ? "#005FB8" : "#EAEAEA"); b.Foreground = FigmaDialogElements.Brush(active ? "#003E92" : "#5F5F5F");
            AutomationProperties.SetHelpText(b, active ? "선택됨" : "");
        }
    }

    private FrameworkElement Footer()
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 24, 0) };
        var close = Button("닫기", () => { Close(); return Task.CompletedTask; });
        var evidenceButton = Button("적용 규정", () => { OpenEvidence(); return Task.CompletedTask; }, true);
        // Enter belongs to the query, page number and focused tree control.
        evidenceButton.IsDefault = false;
        foreach (var b in new[] { close, evidenceButton }) { b.Width = 88; b.Template = FigmaDialogElements.SurfaceTemplate(4); buttons.Children.Add(b); }
        return new Border { Height = 64, Background = FigmaDialogElements.Brush("#F3F3F3"), BorderBrush = FigmaDialogElements.Brush("#EAEAEA"), BorderThickness = new(0, 1, 0, 0), Child = buttons };
    }

    private void PopulatePager()
    {
        previous = Button("이전", () => RunAsync(() => MovePage(page - 1)));
        next = Button("다음", () => RunAsync(() => MovePage(page + 1)));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var b in new[] { previous, next }) { b.Width = 88; b.Margin = new(0); b.Template = FigmaDialogElements.SurfaceTemplate(4); }
        AutomationProperties.SetName(pageInput, "규정 검색 페이지");
        var field = FigmaDialogElements.NumberField(pageInput, pageInput, "규정 검색 페이지", delta => _ = RunAsync(() => MovePage(page + delta)));
        field.Margin = new(8, 0, 0, 0);
        buttons.Children.Add(previous); buttons.Children.Add(field); buttons.Children.Add(pageTotal); buttons.Children.Add(next); pager.Children.Add(buttons);
        pageInput.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return; e.Handled = true;
            if (!int.TryParse(pageInput.Text, out var target) || target < 1 || target > totalPages) { Status.Text = $"1–{totalPages}쪽을 입력해 주세요."; return; }
            await RunAsync(() => MovePage(target - 1));
        };
    }

    private Task MovePage(int target) => target >= 0 && target < totalPages && target != page ? SearchAsync(target) : Task.CompletedTask;

    private Task Submit()
    {
        submitted = query.Text.Trim(); page = 0;
        if (submitted.Length > 0) return SearchAsync();
        rankedMatches = null; ClearReader(); BuildTree(); return Task.CompletedTask;
    }

    private void ClearReader()
    {
        Status.Text = ""; RuleResults.Children.Clear(); count.Visibility = pager.Visibility = empty.Visibility = Visibility.Collapsed;
        reader.ScrollToTop();
    }

    private void Index(RuleContentsNode node, string parent)
    {
        if (node.RuleId is { } id) { articles[id] = node; breadcrumbs[id] = parent; }
        var path = parent.Length == 0 ? node.Heading : parent + " › " + node.Heading;
        foreach (var child in node.Children) Index(child, path);
    }

    private void BuildTree(IReadOnlySet<string>? hits = null)
    {
        updatingTree = true;
        try
        {
            contents.Items.Clear(); articleItems.Clear();
            foreach (var doc in documents) contents.Items.Add(Branch(doc, hits, 0));
            if (contents.IsLoaded && contents.Template.FindName("TreeScroll", contents) is ScrollViewer scroll) scroll.ScrollToTop();
        }
        finally { updatingTree = false; }
    }

    private TreeViewItem Branch(RuleContentsNode node, IReadOnlySet<string>? hits, int depth)
    {
        var text = new TextBlock { Text = node.TreeLabel, TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = node.RuleId is null ? 14 : 12, FontWeight = depth == 0 ? FontWeights.SemiBold : FontWeights.Normal };
        var row = new DockPanel();
        var item = new TreeViewItem { Header = row, Tag = node, Style = branchStyle, ToolTip = node.TreeLabel, Padding = new(8 + depth * 16, 0, 8, 0) };
        if (node.RuleId is { } id && (hits is null || hits.Contains(id))) articleItems[id] = item;
        AutomationProperties.SetName(item, node.TreeLabel);
        var found = node.RuleId != null && hits?.Contains(node.RuleId) == true ? 1 : 0;
        foreach (var child in node.Children)
        {
            var nested = Branch(child, hits, depth + 1);
            found += (int)nested.Resources["MatchCount"];
            if (hits is null || (int)nested.Resources["MatchCount"] > 0) item.Items.Add(nested);
        }
        item.Resources["MatchCount"] = found;
        if (hits is not null && found > 0)
        {
            var number = new TextBlock { Text = found.ToString(), FontSize = 12, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border { Height = 18, CornerRadius = new(9), Padding = new(8, 0, 8, 0), Margin = new(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, Background = FigmaDialogElements.Brush("#005FB8"), Child = number };
            DockPanel.SetDock(badge, Dock.Right); row.Children.Add(badge);
        }
        row.Children.Add(text);
        return item;
    }

    private void RevealTreeItem(string id, bool select)
    {
        if (!articleItems.TryGetValue(id, out var item)) return;
        updatingTree = true;
        try
        {
            for (var parent = ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem; parent != null;
                 parent = ItemsControl.ItemsControlFromItemContainer(parent) as TreeViewItem) parent.IsExpanded = true;
            if (select) item.IsSelected = true;
            item.BringIntoView();
        }
        finally { updatingTree = false; }
    }

    private bool ExpandResult(string id)
    {
        var item = RuleResults.Children.OfType<Expander>().FirstOrDefault(card => Equals(card.Tag, id));
        if (item is null) return false;
        item.IsExpanded = true;
        reader.UpdateLayout();
        reader.ScrollToVerticalOffset(item.TranslatePoint(new Point(), RuleResults).Y);
        return true;
    }

    private void ShowArticle(RuleContentsNode node)
    {
        ClearReader();
        var path = breadcrumbs.GetValueOrDefault(node.RuleId ?? "", "");
        RuleResults.Children.Add(new TextBlock { Text = path, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = FigmaDialogElements.Brush("#8A8A8A") });
        RuleResults.Children.Add(new TextBlock { Text = node.Heading, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new(0, 12, 0, 12), TextWrapping = TextWrapping.Wrap });
        RuleResults.Children.Add(ArticleBody(node, node.Summary ?? ""));
        if (node.Translatable == false) RuleResults.Children.Add(new TextBlock { Text = "점역 미지원 · 검색 전용" });
    }

    private UIElement ArticleBody(RuleContentsNode? node, string fallback, string? excerpt = null)
    {
        if (!string.IsNullOrWhiteSpace(node?.Markdown)) return RuleArticle.Render(node.Markdown, (FontFamily)FindResource("BraillePlainFont"), excerpt);
        return new TextBlock { Text = string.IsNullOrWhiteSpace(node?.LegacyText) ? fallback : node.LegacyText, FontSize = 14, TextWrapping = TextWrapping.Wrap };
    }

    private void ShowEvidence()
    {
        ClearReader();
        Display(evidence.Select(r => new BrailleRule(r.RuleId, r.Source, r.Section, r.Title, r.Excerpt)).ToArray(), useCatalog: false);
        Status.Text = evidence.Length == 0 ? "이 블록에 전달된 적용 규정이 없습니다." : "원문 수신 당시의 적용 규정입니다. 검색으로 현재 본문을 확인할 수 있습니다.";
    }

    private void OpenEvidence()
    {
        if (Owner is AppliedRulesDialog) Close();
        else if (OpenAppliedRules != null) OpenAppliedRules(this);
        else ShowEvidence();
    }

    private async Task SearchAsync(int targetPage = 0, string? selectedId = null)
    {
        var response = await client.SearchPageAsync(submitted, Codes[filterIndex], targetPage, SearchPageSize, Lifetime.Token, includeMatches: true);
        page = targetPage;
        rankedMatches = response.MatchingIds;
        ClearReader();
        if (response.Items.Length == 0) { empty.Visibility = Visibility.Visible; BuildTree(); return; }
        // Older servers omit full match IDs. Keep their complete, browsable tree instead of showing partial counts.
        BuildTree(response.MatchingIds?.ToHashSet(StringComparer.Ordinal));
        Display(response.Items, useCatalog: true);
        totalPages = Math.Max(1, response.TotalPages);
        previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < totalPages; pageInput.Text = (page + 1).ToString(); pageTotal.Text = "/ " + totalPages;
        count.Text = $"{(filterIndex > 0 ? "「" + sources[filterIndex] + "」 · " : "")}「{submitted}」 {response.TotalElements}건 중 {page * SearchPageSize + 1}–{page * SearchPageSize + response.Items.Length}";
        count.Visibility = pager.Visibility = Visibility.Visible;
        if (selectedId != null && ExpandResult(selectedId)) return;
        if (rankedMatches is not null) RevealTreeItem(response.Items[0].RuleId, select: false);
    }

    private void Display(IEnumerable<BrailleRule> rows, bool useCatalog)
    {
        Expander? expanded = null;
        foreach (var row in rows)
        {
            var node = useCatalog ? articles.GetValueOrDefault(row.RuleId) : null;
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = node?.Number ?? row.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            header.Children.Add(new TextBlock { Text = breadcrumbs.GetValueOrDefault(row.RuleId, row.Source + " › " + row.Section), Margin = new(0, 4, 0, 4), Foreground = FigmaDialogElements.Brush("#8A8A8A"), TextWrapping = TextWrapping.Wrap });
            var excerpt = new TextBlock { Text = row.Contents, FontSize = 14, Foreground = FigmaDialogElements.Brush("#5F5F5F"), MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap };
            header.Children.Add(excerpt);
            if (row.Supported == false) header.Children.Add(new TextBlock { Text = row.SupportLabel, Foreground = FigmaDialogElements.Brush("#5F5F5F") });
            var detail = new StackPanel();
            if (!row.HasFullText && string.IsNullOrWhiteSpace(node?.Markdown))
            {
                detail.Children.Add(new TextBlock { Text = "발췌 · 전문 미제공", Foreground = FigmaDialogElements.Brush("#5F5F5F") });
                if (row.RuleId.Length > 0) detail.Children.Add(Button("규정 전문 찾기", () => RunAsync(async () => { query.Text = row.RuleId; filterIndex = 0; PaintFilters(); await Submit(); })));
            }
            detail.Children.Add(ArticleBody(node, row.ExpandedText, row.Contents));
            var item = new Expander { Header = header, Content = detail, Tag = row.RuleId, Margin = new(0, 0, 0, 12), Style = cardStyle };
            AutomationProperties.SetName(item, row.Section + " " + row.Name);
            item.Expanded += (_, _) =>
            {
                if (expanded != null && expanded != item) expanded.IsExpanded = false;
                expanded = item;
                excerpt.MaxHeight = double.PositiveInfinity;
                excerpt.TextTrimming = TextTrimming.None;
                if (useCatalog) RevealTreeItem(row.RuleId, select: true);
                if (string.IsNullOrWhiteSpace(node?.Markdown) && !string.IsNullOrEmpty(row.Contents) && row.ExpandedText.StartsWith(row.Contents, StringComparison.Ordinal))
                    excerpt.Visibility = Visibility.Collapsed;
            };
            item.Collapsed += (_, _) =>
            {
                excerpt.MaxHeight = 40;
                excerpt.TextTrimming = TextTrimming.CharacterEllipsis;
                excerpt.Visibility = Visibility.Visible;
                if (expanded != item) return;
                expanded = null;
                if (useCatalog && articleItems.TryGetValue(row.RuleId, out var entry)) entry.IsSelected = false;
            };
            RuleResults.Children.Add(item);

        }
    }

}
