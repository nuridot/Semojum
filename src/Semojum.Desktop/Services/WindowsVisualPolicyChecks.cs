using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Semojum.Core;

namespace Semojum.Desktop.Services;

internal static class WindowsVisualPolicyChecks
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var window = new MainWindow(smoke: true, testWithoutAccount: true); window.Show();
        try
        {
            var block = window.Session.ActiveBlock!;
            block.Metadata = new(Type: "image", SelectedIndex: 2, Alternatives:
                [new("생략", "생략 본문", null, "⠁", null), new("상세", "상세 본문", null, "⠃", null), new("별책 참조", "참조 본문", null, "⠉", null)]);
            var drafts = block.Metadata.Alternatives;
            window.ApplyVisualDescriptionPolicy(new() { ["figure"] = new(["상세"], "상세") });
            await window.ExecuteAsync("drafts"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var choices = window.DraftChoiceGrid.Children.OfType<RadioButton>().ToArray();
            check(choices.Length == 1 && Equals(choices[0].Tag, 1) && window.DraftTextPreview.Text == "상세 본문",
                "QA1110: only enabled description types appear in the current document's draft dock");
            check(block.Metadata.Alternatives == drafts && block.Metadata.SelectedIndex == 2,
                "QA1110: hiding a draft preserves its original data and selection until apply");
            window.ApplyVisualDescriptionPolicy(new() { ["figure"] = new(["생략", "별책 참조"], "별책 참조") });
            check(!window.DraftDock.IsVisible, "QA1110: changing description policy closes stale draft choices");
            await window.ExecuteAsync("drafts"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            choices = window.DraftChoiceGrid.Children.OfType<RadioButton>().ToArray();
            check(choices.Select(c => (int)c.Tag).SequenceEqual([0, 2]) && choices[1].IsChecked == true && window.DraftTextPreview.Text == "참조 본문",
                "QA1110: re-enabled choices retain source indices and the saved selection");
            window.DraftDockClose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(block.Metadata.SelectedIndex == 2 && block.Source == "참조 본문" && block.Metadata.Alternatives?.Length == 3,
                "QA1110: applying a filtered choice uses its original index and preserves all drafts");
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "qa-policy-" + Guid.NewGuid() + ".smj");
            try
            {
                await SmjFile.SaveAsync(path, SmjFile.Snapshot(window.Session));
                var restored = await SmjFile.LoadAsync(path);
                check(restored.Options.VisualDescriptions!["figure"].Options.SequenceEqual(["생략", "별책 참조"])
                    && restored.Blocks.Single(b => b.Id == block.Id).Metadata.Alternatives?.Length == 3,
                    "QA1110: SMJ preserves enabled description types and hidden alternatives");
            }
            finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            var numberedPolicy = new BrailleOptions(VisualDescriptions: new() { ["figure"] = new(["상세"], "상세") });
            check(VisualDraftSelection.IsEnabled(new(Type: "image"), numberedPolicy, new("새 설명 이름", "", null, "", null, Option: 2))
                && !VisualDraftSelection.IsEnabled(new(Type: "image"), numberedPolicy, new("새 생략 이름", "", null, "", null, Option: 1)),
                "QA1110: numbered server descriptions respect enabled types even after label changes");
            var initial = new EditorBlock { Source = "원문", Metadata = new(Type: "image", Alternatives:
                [new("짧은 제목", "꺼진 제목", null, "", null, Option: 2), new("상세", "선택한 상세", null, "", null, Option: 2)]) };
            check(VisualDraftSelection.ApplyInitial(initial, numberedPolicy) && initial.Source == "선택한 상세",
                "QA1110: initial choice never selects a disabled legacy style sharing the same server option number");
            var options = new BrailleOptions(VisualDescriptions: new() { ["table"] = new(["행열 바꿈"], "행열 바꿈") });
            check(VisualDraftSelection.IsEnabled(new(Type: "table"), options, new("표", "", null, "", null), "transposed")
                && !VisualDraftSelection.IsEnabled(new(Type: "table"), options, new("표", "", null, "", null), "table_grid"),
                "QA1110: table choices follow render-mode description toggles");
            var byId = new BrailleOptions(VisualDescriptions: new() { ["diagram"] = new(["저장 시 이름"], "저장 시 이름", [13], 13) });
            var renamed = new EditorBlock { Source = "원문", Metadata = new(Type: "diagram", Alternatives:
                [new("저장 시 이름", "다른 초안", null, null, null, Option: 9), new("바뀐 이름", "13번 본문", null, null, null, Option: 13)]) };
            check(!VisualDraftSelection.IsEnabled(renamed.Metadata, byId, renamed.Metadata.Alternatives![0])
                && VisualDraftSelection.ApplyInitial(renamed, byId) && renamed.Source == "13번 본문",
                "A renamed diagram draft is filtered and selected by its catalog ID");
            var tablePolicy = new BrailleOptions(VisualDescriptions: new() { ["table"] = new(["번호 체계"], "번호 체계", [5], 5) });
            var table = new EditorBlock { Source = "표", Metadata = new(Type: "table") };
            check(VisualDraftSelection.ApplyInitial(table, tablePolicy) && table.Metadata.RenderMode == "numbered",
                "Catalog table option 5 selects numbered table rendering");
            var numbered = new BrailleOptions(VisualOptions: new() { ["chart_graph"] = new([13], 0) });
            renamed.Metadata = renamed.Metadata with { Type = "image", VisualSubtype = "chart_graph", SelectedIndex = 0 };
            check(!VisualDraftSelection.IsEnabled(renamed.Metadata, numbered, renamed.Metadata.Alternatives![0])
                && VisualDraftSelection.IsEnabled(renamed.Metadata, numbered, renamed.Metadata.Alternatives[1])
                && !VisualDraftSelection.ApplyInitial(renamed, numbered) && renamed.Metadata.SelectedIndex == 0,
                "Numeric filters use the material subtype and zero preserves the AI selection");
            numbered = numbered with { VisualOptions = new() { ["chart_graph"] = new([13], 13) } };
            check(VisualDraftSelection.ApplyInitial(renamed, numbered) && renamed.Metadata.SelectedIndex == 1,
                "A numeric default selects its draft even after a label changes");
            numbered = numbered with { VisualOptions = new() { ["chart_graph"] = new([], 0) } };
            check(VisualDraftSelection.IsEnabled(renamed.Metadata, numbered, renamed.Metadata.Alternatives[0])
                && VisualDraftSelection.IsEnabled(table.Metadata, numbered, new("표", "", null, "", null), "table_grid"),
                "Empty and omitted categories both allow all drafts");
            var form = new BrailleOptions(VisualOptions: new() { ["table"] = new([3], 3) });
            var previous = new BrailleOptions(VisualOptions: new() { ["image"] = new([6], 6) });
            var replacement = VisualDescriptions.ReplaceForJob(previous, form).VisualOptions!;
            check(replacement["image"].Options.Length == 0 && replacement["table"].DefaultOption == 3
                && !form.VisualOptions!.ContainsKey("image"), "Switching forms clears previous job restrictions without changing the form");
        }
        finally { await window.ReleaseResourcesAsync(); window.PrepareSmokeShutdown(); window.Close(); }
    }
}
