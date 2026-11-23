using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Semojum.Core;
using Semojum.Desktop.Dialogs;
using SharpVectors.Converters;
using static Semojum.Desktop.Services.WindowsExportRangeChecks;

namespace Semojum.Desktop.Services;

internal static class WindowsMemoFrameChecks
{
    internal static async Task RunAsync(string directory, Action<bool, string> check)
    {
        var shots = Directory.CreateDirectory(Path.Combine(directory, "memo")).FullName;
        var editor = new MainWindow(smoke: true, testWithoutAccount: true);
        editor.Show();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetField("autoTranslate", flags)!.SetValue(editor, false);
            var document = new DocumentSession { IsTextJob = true };
            var at = DateTimeOffset.Now;
            var lines = string.Join("\n", Enumerable.Repeat("본문", 18));
            var cells = string.Join("\n", Enumerable.Repeat("⠁", 18));
            foreach (var page in new[] { 12, 13, 47 })
                document.Add(new EditorBlock { Id = page, Page = page, Source = lines, Braille = cells, HasManualBraille = true });
            var first = document.Blocks[0];
            first.Memos.Add(new("one", null, "TEXT", 3, "본문", "(가) 지점 표기를 원본과 맞출 것", at));
            first.Memos.Add(new("two", null, "BRAILLE", 6, "⠁", "줄바꿈 위치 다시 볼 것", at));
            document.Blocks[1].Memos.Add(new("three", null, "TEXT", 1, "본문", "그림 설명 초안 2안이 더 맞음", at));
            document.Blocks[2].Memos.Add(new("four", null, "BRAILLE", 17, "⠁", "표 테두리 확인", at));
            document.Blocks[2].Memos.Add(new("five", null, "TEXT", 1, "본문", "완료된 검토", at, Resolved: true));
            typeof(MainWindow).GetMethod("SetSession", flags)!.Invoke(editor, [document, null, false, false]);

            var openList = InspectModal<MemoListDialog>(editor, async list =>
            {
                list.MemoItems.SelectedIndex = 1;
                list.UpdateLayout();
                check(list.RenderSize == new Size(640, 316) && list.MemoItems.Items.Count == 4 &&
                    Tree(list).OfType<TextBlock>().Any(t => t.Text == "점자 18줄"), "The editor memo command shows four Figma rows with separate source and braille coordinates");
                await WindowsSmoke.RenderAsync(list, Path.Combine(shots, "list.png"));
                list.ShowResolved.IsChecked = true;
                check(list.MemoItems.Items.Count == 5 && list.MemoItems.SelectedIndex == 1, "Showing resolved memos preserves the selected memo");
                list.ShowResolved.IsChecked = false;
                check(list.MemoItems.Items.Count == 4 && list.MemoItems.SelectedIndex == 1, "Hiding resolved memos retains the active row");
            });
            await editor.ExecuteAsync("memos");
            await openList;

            const string draft = "여기 그래프랑 본문이 안 맞는 것 같은데";
            var input = new MemoInputDialog("김점역") { Owner = editor };
            var submit = InspectModal<MemoInputDialog>(editor, async dialog =>
            {
                dialog.Input.Text = draft;
                var cancel = Button(dialog, "취소");
                var save = Button(dialog, "저장");
                cancel.Focus();
                check(dialog.RenderSize == new Size(320, 158) && Bounds(dialog.Input, dialog) == new Rect(13, 41, 294, 64) &&
                    Bounds(cancel, dialog) == new Rect(123, 113, 88, 32) && Bounds(save, dialog) == new Rect(219, 113, 88, 32),
                    $"The new memo frame, text field and buttons match Figma at 96 DPI ({dialog.RenderSize}; {Bounds(dialog.Input, dialog)}; {Bounds(save, dialog)})");
                await WindowsSmoke.RenderAsync(dialog, Path.Combine(shots, "input.png"));
                Click(save);
            });
            var saved = input.ShowDialog(); await submit;
            check(saved == true && input.Value == draft, "The Figma input saves typed Korean through its primary action");

            const string body = "원문은 「작다」인데 그래프 보면 「크다」가 맞는 것 같아요.";
            const string reply = "맞네요. 출판사에 물어보고 답 오면 알려 드릴게요.";
            var root = new LineMemo("thread", null, "TEXT", 0, "본문", body, at.AddMinutes(-10),
                Author: "김점역", Replies: [new("이교정", reply, at.AddMinutes(-3))]);
            first.Memos.Add(root);
            var thread = new MemoThreadDialog(first, "TEXT", 0, "본문", editor.EditStructure, document, author: "김점역") { Owner = editor };
            thread.Show(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); thread.UpdateLayout();
            var field = Bounds(thread.ReplyInput, thread);
            check(thread.RenderSize == new Size(360, 178) && field == new Rect(13, 137, 294, 32) &&
                Bounds(thread.ReplyButton, thread) == new Rect(315, 137, 32, 32),
                $"Two single-line messages and the reply composer match the Figma thread ({thread.RenderSize}; {field})");
            await WindowsSmoke.RenderAsync(thread, Path.Combine(shots, "thread.png"));
            var menu = thread.CommandMenu;
            menu.PlacementTarget = thread.MemoItems; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); menu.UpdateLayout();
            var items = menu.Items.OfType<MenuItem>().ToArray();
            check(menu.RenderSize == new Size(248, 284) && items.Select(i => i.Header).SequenceEqual(
                    new[] { "해결", "이전 메모", "다음 메모", "메모 목록", "회신 추가", "새 메모", "선택 수정", "닫기" }) &&
                items.All(i => i.Icon is SvgViewbox { Drawings.Bounds.IsEmpty: false }),
                $"All eight memo commands and their vector icons fit the 248×284 Figma menu ({menu.RenderSize})");
            await WindowsSmoke.RenderElementAsync(menu, Path.Combine(shots, "menu.png"));
            menu.IsOpen = false;
            void Command(string label) => items.Single(i => Equals(i.Header, label)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var replyLabel = Tree(thread).OfType<TextBlock>().Single(t => t.Text == reply);
            replyLabel.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.PreviewMouseDownEvent });
            var edit = InspectModal<MemoInputDialog>(thread, dialog =>
            {
                check(dialog.Input.Text == reply, "The edit command opens the reply selected with the right mouse button");
                dialog.Input.Text = "회신 수정"; Click(Button(dialog, "저장")); return Task.CompletedTask;
            });
            Command("선택 수정"); await edit;
            var result = first.Memos.Single(m => m.LocalId == root.LocalId);
            check(result.Body == body && result.Replies is [{ Body: "회신 수정", Author: "이교정" }] &&
                result.Replies[0].CreatedAt == root.Replies![0].CreatedAt, "Editing a reply retains the original author, timestamp and root message");
            Command("회신 추가");
            check(thread.ReplyInput.IsKeyboardFocusWithin, "Add reply focuses the empty composer");
            thread.ReplyInput.Text = "추가 회신"; Command("회신 추가");
            check(first.Memos.Single(m => m.LocalId == root.LocalId).Replies is [_, { Body: "추가 회신", Author: "김점역" }],
                "The menu appends a composed reply exactly once");
            var overview = InspectModal<MemoListDialog>(thread, list =>
            {
                list.MemoItems.SelectedIndex = 2;
                Click(Button(list, "이동")); return Task.CompletedTask;
            });
            Command("메모 목록"); await overview;
            check(!thread.IsVisible && thread.DestinationMemoId == "two" && thread.Destination is { Panel: SearchPanel.Braille },
                "The thread's overview returns the chosen memo ID and braille location");

            first.Memos.Add(new("same-line", null, "BRAILLE", 6, "⠁", "같은 줄의 다른 메모", at.AddSeconds(1)));
            var choose = InspectModal<MemoListDialog>(editor, list =>
            {
                list.MemoItems.SelectedIndex = 3;
                Click(Button(list, "이동")); return Task.CompletedTask;
            });
            var follow = editor.Dispatcher.InvokeAsync(async () =>
            {
                for (var i = 0; i < 100; i++)
                {
                    var next = editor.OwnedWindows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w is not MemoListDialog);
                    if (next is null) { await Task.Delay(25); continue; }
                    try { check(next is MemoThreadDialog { MemoItems.SelectedItem: LineMemo { LocalId: "same-line" } },
                        "Editor navigation reopens the selected braille memo even when two memos occupy the same line"); }
                    finally { next.Close(); }
                    return;
                }
                throw new InvalidOperationException("Editor did not reopen the chosen memo");
            }, DispatcherPriority.ApplicationIdle);
            await editor.ExecuteAsync("memos");
            await Task.WhenAll(choose, follow.Task.Unwrap());
        }
        finally { await editor.ReleaseResourcesAsync(); editor.PrepareSmokeShutdown(); editor.Close(); }
    }

    private static Task InspectModal<T>(Window owner, Func<T, Task> inspect) where T : Window =>
        owner.Dispatcher.InvokeAsync(async () =>
        {
            var dialog = owner.OwnedWindows.OfType<T>().Single(w => w.IsVisible);
            try { dialog.UpdateLayout(); await inspect(dialog); }
            finally { if (dialog.IsVisible) dialog.Close(); }
        }, DispatcherPriority.ApplicationIdle).Task.Unwrap();

    private static Rect Bounds(FrameworkElement element, Window owner) => new(element.TranslatePoint(new Point(), owner), element.RenderSize);
    private static Button Button(Window window, string label) => Tree(window).OfType<Button>().Single(b => Equals(b.Content, label));
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
}
