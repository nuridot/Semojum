using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Semojum.Core.Integration;
using Semojum.Desktop.Dialogs;
using Semojum.Feature.Rules;

namespace Semojum.Desktop.Services;

internal static class WindowsRuleCatalogChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("RULE_CATALOG_QA_ORIGIN") is not { Length: > 0 } origin) return;
        var service = new Uri(origin);
        if (!service.IsLoopback) throw new InvalidOperationException("규정 통합 검사는 로컬 시험 서버에서만 실행합니다.");
        using var http = new HttpClient();
        var api = new SemojumApiClient(http, service);
        await api.LoginAsync(Environment.GetEnvironmentVariable("RULE_CATALOG_QA_LOGIN")!, Environment.GetEnvironmentVariable("RULE_CATALOG_QA_PASSWORD")!);
        var dialog = new RuleDialog(api, [], "", "");
        dialog.Show();
        try
        {
            var tree = Elements(dialog).OfType<TreeView>().Single();
            await Until(() => tree.Items.Count == 3);
            var leaves = Nodes(tree).Where(item => ((RuleContentsNode)item.Tag).RuleId != null).ToArray();
            check(leaves.Length == 340 && leaves.All(item => !string.IsNullOrEmpty(((RuleContentsNode)item.Tag).Markdown)), "Authenticated catalog supplies all 340 Markdown articles across three documents");
            check(dialog.RuleResults.Children.Count == 0 && tree.Items.Cast<TreeViewItem>().All(item => !item.IsExpanded), "Opening the browser shows three collapsed document roots and an empty reader");
            await Capture("start");
            var sample = leaves.Single(item => ((RuleContentsNode)item.Tag).RuleId == "MCST-한글-6.13.55");
            Select(sample);
            check(Elements(dialog.RuleResults).OfType<TextBlock>().Any(t => t.Text == "제55항") &&
                Elements(dialog.RuleResults).OfType<TextBlock>().Any(t => t.Text == "예시") &&
                Elements(dialog.RuleResults).OfType<TextBlock>().Any(WindowsBrailleFontChecks.DrawsPlainBrailleFont), "Selecting article 55 displays its heading, example and raised-only braille typeface");
            await Capture("contents");
            var articleScroll = Elements(dialog).OfType<ScrollViewer>().Single(s => s.Content == dialog.RuleResults);
            var bar = (System.Windows.Controls.Primitives.ScrollBar)articleScroll.Template.FindName("PART_VerticalScrollBar", articleScroll);
            System.Windows.Controls.Primitives.ScrollBar.PageDownCommand.Execute(null, bar);
            await Until(() => articleScroll.VerticalOffset > 0);
            articleScroll.ScrollToEnd();
            await Until(() => Math.Abs(articleScroll.ScrollableHeight - articleScroll.VerticalOffset) < 1);
            check(articleScroll.ScrollableHeight > 0, "The rule reader scrolls through the full official examples with the overlay rail");
            articleScroll.ScrollToHome();
            var table = leaves.First(item => System.Text.RegularExpressions.Regex.IsMatch(((RuleContentsNode)item.Tag).Markdown!, @"(?m)^\|(?: *:?-{3,}:? *\|)+\r?$"));
            Select(table);
            check(Elements(dialog.RuleResults).OfType<Grid>().Any(g => g.RowDefinitions.Count > 1 && g.ColumnDefinitions.Count > 1),
                "An authenticated article with a verified GFM table renders real rows and columns");
            var query = Elements(dialog).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "규정 검색어");
            async Task Search(string term, Func<bool> done)
            {
                query.Text = term; query.Focus();
                var source = PresentationSource.FromVisual(query)!;
                query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter) { RoutedEvent = UIElement.KeyDownEvent });
                await Until(done);
            }
            await Search("빗금", () => dialog.RuleResults.Children.OfType<Expander>().Any());
            var results = dialog.RuleResults.Children.OfType<Expander>().ToArray();
            check(results.Length == 4, "The first search page displays four server-ranked hits and document match counts");
            var target = Nodes(tree).Single(item => ((RuleContentsNode)item.Tag).RuleId == "MCST-한글-6.13.55");
            Select(target);
            var selectedCard = () => dialog.RuleResults.Children.OfType<Expander>().SingleOrDefault(card => card.IsExpanded);
            await Until(() => Equals(selectedCard()?.Tag, "MCST-한글-6.13.55"));
            var pageField = Elements(dialog).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "규정 검색 페이지");
            check(pageField.IsVisible && dialog.RuleResults.Children.OfType<Expander>().Count() == 4 && target.IsSelected,
                "Selecting a matching article in the tree expands its search card and keeps its page and other results");
            check(Elements(tree).OfType<Border>().Any(b => b.IsVisible && b.Height == 18 && b.CornerRadius.TopLeft == 9
                && b.Background is SolidColorBrush { Color: var color } && color == Color.FromRgb(0,95,184)),
                "Visible search counts appear in the blue 18px tree badges");
            var down = Elements(dialog).OfType<System.Windows.Controls.Primitives.RepeatButton>()
                .Single(b => AutomationProperties.GetName(b) == "규정 검색 페이지 늘리기");
            check(Math.Abs((pageField.TranslatePoint(new Point(), dialog).X + pageField.ActualWidth / 2) -
                (dialog.ActualWidth + 340) / 2) < 22, "Page navigation is centered under the article reader");
            var summary = ((RuleContentsNode)target.Tag).Summary!;
            check(Elements(selectedCard()!).OfType<TextBlock>().Count(t => t.IsVisible
                && new System.Windows.Documents.TextRange(t.ContentStart, t.ContentEnd).Text.Trim() == summary) == 1,
                "The expanded result displays the article's matching introductory sentence exactly once");
            await Capture("search");
            var firstPage = pageField.Text;
            down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => pageField.Text != firstPage);
            var backToArticle = Nodes(tree).Single(item => ((RuleContentsNode)item.Tag).RuleId == "MCST-한글-6.13.55");
            Select(backToArticle);
            await Until(() => pageField.Text == firstPage && Equals(selectedCard()?.Tag, "MCST-한글-6.13.55"));
            check(pageField.IsVisible && tree.SelectedItem is TreeViewItem { Tag: RuleContentsNode { RuleId: "MCST-한글-6.13.55" } },
                "Selecting a hit on a different result page opens its ranked page and restores tree selection");
            var otherCard = dialog.RuleResults.Children.OfType<Expander>().First(card => card != selectedCard());
            otherCard.IsExpanded = true;
            check(tree.SelectedItem is TreeViewItem { Tag: RuleContentsNode active } && active.RuleId == otherCard.Tag as string,
                "Opening another result updates the corresponding selected article in the tree");
            var sameEntry = (TreeViewItem)tree.SelectedItem;
            otherCard.IsExpanded = false; sameEntry.IsSelected = true;
            check(otherCard.IsExpanded, "A collapsed card can be reopened by selecting the same article in the tree");
            await Search("검증용일치하지않는검색어78245", () => Elements(dialog).OfType<TextBlock>().Any(t => t.Text == "검색 결과가 없습니다." && t.IsVisible));
            check(!dialog.RuleResults.Children.OfType<Expander>().Any(), "A search with no matches clears results and shows the empty state");
            await Capture("empty");
            var clear = Elements(dialog).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "RuleSearchClear");
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => query.Text.Length == 0 && tree.Items.Count == 3 && dialog.RuleResults.Children.Count == 0);
            check(tree.Items.Cast<TreeViewItem>().All(i => !i.IsExpanded), "Clearing the query restores the initial document browser");

            const string longArticleId = "MCST-한글-4.10.33";
            dialog.Width = 640;
            await Search(longArticleId, () => dialog.RuleResults.Children.OfType<Expander>().Any(card => Equals(card.Tag, longArticleId)));
            var longCard = dialog.RuleResults.Children.OfType<Expander>().Single(card => Equals(card.Tag, longArticleId));
            var firstParagraph = ((RuleContentsNode)leaves.Single(item => ((RuleContentsNode)item.Tag).RuleId == longArticleId).Tag).Summary!;
            var preview = Elements(longCard).OfType<TextBlock>().Single(t => t.Text == firstParagraph && t.IsVisible);
            var collapsedHeight = preview.ActualHeight;
            longCard.IsExpanded = true;
            dialog.UpdateLayout();
            var fullText = new TextBlock { Text = preview.Text, FontFamily = preview.FontFamily, FontSize = preview.FontSize, TextWrapping = TextWrapping.Wrap };
            fullText.Measure(new Size(preview.ActualWidth, double.PositiveInfinity));
            check(preview.IsVisible && preview.ActualHeight > collapsedHeight && preview.ActualHeight + 1 >= fullText.DesiredSize.Height,
                "Opening article 33 at 640 DIP shows its complete first paragraph beyond the two-line preview");
            await Capture("narrow-article");
            longCard.IsExpanded = false;
            dialog.UpdateLayout();
            check(Math.Abs(preview.ActualHeight - collapsedHeight) < 1, "Collapsing the article restores its compact search preview");
        }
        finally { dialog.Close(); }

        async Task Until(Func<bool> done)
        {
            var limit = DateTime.UtcNow.AddSeconds(15);
            while (!done() && DateTime.UtcNow < limit) { await Task.Delay(30); dialog.UpdateLayout(); }
            if (!done()) throw new InvalidOperationException("규정 창의 예상 상태를 확인할 수 없습니다.");
        }
        async Task Capture(string state) => await WindowsSmoke.RenderElementAsync(dialog, Path.Combine(directory, state + ".png"));
        void Select(TreeViewItem item)
        {
            for (var parent = ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem; parent != null; parent = ItemsControl.ItemsControlFromItemContainer(parent) as TreeViewItem)
                parent.IsExpanded = true;
            item.IsSelected = true; item.BringIntoView(); dialog.UpdateLayout();
        }
    }
    private static IEnumerable<TreeViewItem> Nodes(ItemsControl parent)
    {
        foreach (TreeViewItem item in parent.Items)
        {
            yield return item;
            foreach (var child in Nodes(item)) yield return child;
        }
    }
    private static IEnumerable<DependencyObject> Elements(DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Elements(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
}
