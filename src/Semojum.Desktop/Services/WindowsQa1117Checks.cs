using System.Data;
using System.IO;
using System.Reflection;
using System.Net.Http;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Core.Integration;
using Semojum.Feature.Board;
using Semojum.Desktop.Controls;
using Semojum.Desktop.Dialogs;

namespace Semojum.Desktop.Services;

internal static class WindowsQa1117Checks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        foreach (var weight in new[] { FontWeights.Normal, FontWeights.SemiBold, FontWeights.Bold })
        {
            var face = new Typeface(BundledFonts.Interface, FontStyles.Normal, weight, FontStretches.Normal);
            var resolved = face.TryGetGlyphTypeface(out var glyph);
            check(resolved && glyph.FontUri.LocalPath.Contains("Pretendard", StringComparison.OrdinalIgnoreCase)
                && glyph.CharacterToGlyphMap.ContainsKey(0xAC00) && glyph.CharacterToGlyphMap.ContainsKey('A') && glyph.CharacterToGlyphMap.ContainsKey(0x318D),
                $"Pretendard {weight} supplies Korean, English and U+318D from bundled files (resolved={resolved}, uri={glyph?.FontUri})");
        }
        var window = new MainWindow(smoke: true, testWithoutAccount: true, startEmpty: true);
        window.Show();
        try
        {
            await window.ExecuteAsync("new"); await Idle(window);
            check(window.OriginalUpload.IsVisible, "New document displays the original upload component");
            var upload = window.OriginalUpload.UploadArea;
            check(upload.ActualHeight >= window.OriginalUpload.ActualHeight - 65 && upload.ActualWidth >= window.OriginalUpload.ActualWidth - 33,
                $"Upload target fills original panel (target={upload.ActualWidth}x{upload.ActualHeight}, panel={window.OriginalUpload.ActualWidth}x{window.OriginalUpload.ActualHeight})");
            window.Height = 700; await Idle(window);
            check(upload.ActualHeight >= window.OriginalUpload.ActualHeight - 65, "Upload target follows resized panel height");
            await WindowsSmoke.RenderElementAsync(window, Path.Combine(directory, "new-document.png"));

            var memo = new MemoInputDialog("작성자") { Owner = window }; memo.Show(); await Idle(memo);
            var height = memo.Input.ActualHeight;
            memo.Input.Text = "첫째 줄\n둘째 줄\n셋째 줄\n넷째 줄"; await Idle(memo);
            check(memo.Input.ActualHeight > height + 20, "New memo field grows with multiline input");
            check(memo.Input.GetRectFromCharacterIndex(memo.Input.Text.Length).Bottom <= memo.Input.ActualHeight, "Memo last line remains inside the growing field");
            await WindowsSmoke.RenderElementAsync(memo, Path.Combine(directory, "memo-input.png")); memo.Close();

            var block = window.Session.ActiveBlock!;
            block.Memos.Add(new("qa1117", null, "TEXT", 0, "", "기존 메모", DateTimeOffset.Now));
            var thread = new MemoThreadDialog(block, "TEXT", 0, "", window.EditStructure, window.Session) { Owner = window }; thread.Show(); await Idle(thread);
            height = thread.ReplyInput.ActualHeight; var windowHeight = thread.ActualHeight;
            thread.ReplyInput.Text = "첫째 회신\n둘째 회신\n셋째 회신\n넷째 회신"; await Idle(thread);
            check(thread.ReplyInput.ActualHeight > height + 20 && thread.ActualHeight > windowHeight + 20, "Reply field and memo window expand together");
            await WindowsSmoke.RenderElementAsync(thread, Path.Combine(directory, "memo-reply.png"));
            thread.ReplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(thread);
            check(block.Memos.Single().Replies is [{ Body: "첫째 회신\n둘째 회신\n셋째 회신\n넷째 회신" }] && thread.ReplyInput.Text.Length == 0,
                "Multiline reply is saved once and composer resets");
            thread.Close();
            window.DraftDockTitle.Text = "대체 초안";
            window.DraftDock.Visibility = Visibility.Visible; await Idle(window);
            var thumbBorder = Descendants(window.DraftDockResize).OfType<Border>().Single();
            check(thumbBorder.Background is SolidColorBrush { Color.A: 0 } && thumbBorder.BorderThickness == new Thickness(0), "Draft resize handle paints no dark line above the title");
            await WindowsSmoke.RenderElementAsync(window.DraftDock, Path.Combine(directory, "draft-header.png"));
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }

        await WindowsQa1110SpaceChecks.RunAsync(check);
        await WindowsQa1110TableChecks.RunAsync(check);
        await WindowsAccountChecks.RunAsync(check);
        await WindowsRangeDesignChecks.RunAsync(check);
        await WindowsPublicInquiryFieldsChecks.RunAsync(check);
        await BoardAttachmentsAsync(directory, check);
        await WindowsImeFontChecks.RunAsync(check);
    }
    private static async Task BoardAttachmentsAsync(string directory, Action<bool, string> check)
    {
        using var http = new HttpClient(); var client = new BoardClient(new SemojumApiClient(http));
        var board = new BoardDialog(client, ""); // Do not load a real account or send any requests.
        var post = new BoardPost(1, "문의", "본문", "작성자", true, false, false, "", "", 0,
            [new(1, "첨부 문서.pdf", 10, ""), new(2, "첨부 이미지.png", 10, "")], []);
        var view = new PostEditorView(board, client, post, "");
        var host = new Window { Content = view, Width = 800, Height = 800 }; host.Show();
        try
        {
            await Idle(host);
            var list = Descendants(view).OfType<ListBox>().Single();
            var files = (System.Collections.IList)list.ItemsSource;
            files.Add(Activator.CreateInstance(files[0]!.GetType(), "새 첨부.txt", null, "fixture.txt"));
            await Idle(host);
            var remove = Descendants(list).OfType<Button>().Where(b => AutomationProperties.GetAutomationId(b) == "BoardRemoveAttachment").ToArray();
            check(remove.Length == 3 && Descendants(list).OfType<System.Windows.Shapes.Path>().Count() == 3, "Every existing and new inquiry attachment has a visible icon and remove button");
            remove[1].Focus(); remove[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(host);
            check(list.Items.Count == 2 && list.Items.Cast<object>().All(a => a.ToString() != "첨부 이미지.png"), "Removing an existing inquiry attachment retains the other files");
            Descendants(list).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "첨부 삭제: 새 첨부.txt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(host);
            check(list.Items.Count == 1 && list.Items[0].ToString() == "첨부 문서.pdf", "A newly attached file can be cancelled before sending");
            await WindowsSmoke.RenderElementAsync(host, Path.Combine(directory, "inquiry-attachments.png"));
            Descendants(list).OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(host);
            check(list.Items.Count == 0 && list.Visibility == Visibility.Collapsed, "Removing the last attachment collapses the list");
        }
        finally { host.Close(); board.Close(); }
    }
    private static async Task Idle(Window window) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }
}
