using System.Text.Json;
using Semojum.Core;
using Semojum.Core.Integration;

internal static class EngineDraftChecks
{
    public static IEnumerable<(string Name, Func<Task> Run)> Tests()
    {
        yield return ("First engine draft survives editing, retranslation, frozen save and SMJ reopen", RoundTrip);
        yield return ("Existing server and legacy braille never fabricate an engine draft", UnknownBaseline);
        yield return ("Only engine-produced braille sends engineContents, including an empty first draft", Payload);
    }

    private static async Task RoundTrip()
    {
        var doc = new DocumentSession { JobId = "job-1" };
        var block = new EditorBlock { Id = 1, Page = 1, Source = "원문" };
        doc.Add(block);
        block.ApplyTranslatedBraille("⠁\n⠃");
        block.EditBraille("⠉");
        var frozen = SmjFile.Snapshot(doc);
        block.ApplyTranslatedBraille("⠙");
        Check(block.EngineDraft?.Contents == "⠁\n⠃", "Retranslation replaced the first engine output");
        var plan = JobClient.PlanSave(frozen.Manifest);
        Check(plan.Pages[0].Braille![0].EngineContents!.SequenceEqual(new[] { "⠁", "⠃" }), "Wrong engine draft");
        Check(plan.Pages[0].Braille![0].Contents.SequenceEqual(new[] { "⠉" }), "Frozen final output changed");
        Check(frozen.Manifest.Version == SmjFile.EngineDraftVersion, "Baseline must be protected from older readers");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".smj");
        try
        {
            await SmjFile.SaveAsync(path, frozen);
            var restored = await SmjFile.LoadAsync(path);
            restored.Blocks[0].ApplyTranslatedBraille("⠑");
            Check(restored.Blocks[0].EngineDraft?.Contents == "⠁\n⠃", "Reopen lost the first engine output");
            Check(!restored.Blocks[0].EngineDraft!.CapturePending, "Reopened baseline is mutable");
        }
        finally { File.Delete(path); }
    }

    private static async Task UnknownBaseline()
    {
        using var json = JsonDocument.Parse("""
        {"result":{"text_list":[{"id":"old","contents":["원문"]},{"id":"new","contents":["신규"]}],
         "braille_text_list":[{"id":"old","contents":["⠁"]}]}}
        """);
        var page = JobClient.ReadPage(json.RootElement, 1, 1);
        page.Blocks[0].ApplyTranslatedBraille("⠃");
        page.Blocks[1].ApplyTranslatedBraille("⠉");
        Check(page.Blocks[0].EngineDraft is null, "Server final cells were treated as an engine draft");
        Check(page.Blocks[1].EngineDraft?.Contents == "⠉", "New server block did not capture its first translation");
        var doc = new DocumentSession(); doc.Add(page.Blocks[0]);
        var snapshot = SmjFile.Snapshot(doc);
        var legacy = snapshot with { Manifest = snapshot.Manifest with { Version = 6 } };
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".smj");
        try
        {
            await SmjFile.SaveAsync(path, legacy);
            var restored = await SmjFile.LoadAsync(path);
            restored.Blocks[0].ApplyTranslatedBraille("⠙");
            Check(restored.Blocks[0].EngineDraft is null, "Legacy file manufactured a missing original");
        }
        finally { File.Delete(path); }
    }

    private static Task Payload()
    {
        var doc = new DocumentSession();
        var generated = new EditorBlock { Id = 1, Page = 1 };
        generated.ApplyTranslatedBraille(""); generated.ApplyTranslatedBraille("⠁");
        var manual = new EditorBlock { Id = 2, Page = 1 }; manual.EditBraille("⠃");
        doc.Add(generated); doc.Add(manual);
        var plan = JobClient.PlanSave(SmjFile.Snapshot(doc).Manifest);
        var value = JsonSerializer.SerializeToElement(plan.Pages, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(value[0].GetProperty("braille")[0].GetProperty("engineContents").GetArrayLength() == 0, "Empty first draft was overwritten");
        Check(!value[0].GetProperty("braille")[1].TryGetProperty("engineContents", out _), "Manual cells leaked into engineContents");
        Check(!value[0].GetProperty("text")[0].TryGetProperty("engineContents", out _), "Text panel sent engineContents");
        return Task.CompletedTask;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
