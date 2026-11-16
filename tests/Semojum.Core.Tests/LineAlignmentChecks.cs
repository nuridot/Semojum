using Semojum.Core;
using Semojum.Core.Integration;

internal static class LineAlignmentChecks
{
    public static async Task RunAsync()
    {
        var python = Environment.GetEnvironmentVariable("SEMOJUM_ENGINE_PYTHON");
        if (string.IsNullOrEmpty(python)) { Console.WriteLine("SKIP line alignment: SEMOJUM_ENGINE_PYTHON is not set"); return; }
        var entry = Path.Combine(AppContext.BaseDirectory, "engine-adapter", "engine-entry.py");
        await using var sidecar = new BrailleSidecar(new(python, ["-u", entry])); var engine = new LocalBrailleEngine(sidecar);
        var single = new EditorBlock { Id = 20, Page = 1, Source = "<!가운데정렬>나" };
        if (await new TranslationCoordinator(engine).TranslateAsync(single, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(20)) != TranslationOutcome.Applied)
            throw new Exception("Single-line translation failed");
        var singleDocument = new DocumentSession { Layout = new(Columns: 8, Lines: 4, PageNumbers: "출력 안 함", Footer: "출력 안 함", ShowSourcePageNumber: false, ShowBraillePageNumber: false) };
        singleDocument.Add(single);
        var singleResult = await engine.ComposeAsync(SmjFile.Snapshot(singleDocument).Manifest);
        var singleBrf = await engine.ReadBrfAsync(singleResult.Brf);
        if (singleBrf[0][0] != "⠀⠀⠀⠉") throw new Exception($"Single-line centered BRF did not retain its alignment: {singleBrf[0][0]}");
        if (singleResult.AlignmentUnknownBlockIds is not { Length: 0 }) throw new Exception("An unambiguous single-line alignment was reported as unknown");
        single.Source = "<!오른쪽정렬>나";
        if (await new TranslationCoordinator(engine).TranslateAsync(single, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(20)) != TranslationOutcome.Applied)
            throw new Exception("Single-line right-aligned translation failed");
        var right = await engine.ReadBrfAsync((await engine.ComposeAsync(SmjFile.Snapshot(singleDocument).Manifest)).Brf);
        if (right[0][0] != "⠀⠀⠀⠀⠀⠀⠀⠉") throw new Exception($"Single-line right-aligned BRF is wrong: {right[0][0]}");
        single.EditBraille("⠛");
        var manualSingle = await engine.ComposeAsync(SmjFile.Snapshot(singleDocument).Manifest);
        var manualBrf = await engine.ReadBrfAsync(manualSingle.Brf);
        if (single.Braille != "⠛" || manualBrf[0][0] != "⠀⠀⠀⠀⠀⠀⠀⠛" || manualSingle.AlignmentUnknownBlockIds is not { Length: 0 })
            throw new Exception("Single-line manual braille was changed or lost its right alignment");
        var source = "가\n<!가운데정렬>나\n<!오른쪽정렬>다";
        var block = new EditorBlock { Id = 1, Page = 1, Source = source };
        var outcome = await new TranslationCoordinator(engine).TranslateAsync(block, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(20));
        if (outcome != TranslationOutcome.Applied) throw new Exception("Tagged text translation failed");
        if (block.Translation?.LineAlignment is not [{ Align: "" }, { Align: "center" }, { Align: "right" }]) throw new Exception("Verified engine line alignment is missing");
        var model = new DocumentSession { Layout = new(Columns: 8, Lines: 4, PageNumbers: "출력 안 함", Footer: "출력 안 함", ShowSourcePageNumber: false, ShowBraillePageNumber: false) };
        model.Add(block);
        var path = Path.Combine(Path.GetTempPath(), "semojum-alignment-" + Guid.NewGuid().ToString("N") + ".smj");
        try
        {
            await SmjFile.SaveAsync(path, SmjFile.Snapshot(model)); var loaded = await SmjFile.LoadAsync(path);
            if (loaded.ActiveBlock!.Source != source || loaded.ActiveBlock.Braille != block.Braille) throw new Exception("SMJ changed source tags or braille");
            var result = await engine.ComposeAsync(SmjFile.Snapshot(loaded).Manifest);
            if (loaded.ActiveBlock.Translation?.LineAlignment is not { Length: 3 } || result.AlignmentUnknownBlockIds is not { Length: 0 }) throw new Exception("Verified alignment did not survive SMJ reopen");
            if (result.Pages![0].Lines.Take(3).ToArray() is not ["⠀⠀⠫", "⠀⠀⠀⠉", "⠀⠀⠀⠀⠀⠀⠀⠊"]) throw new Exception("Distinct line alignments did not appear in the composed BRF");
            var manual = loaded.ActiveBlock; manual.HasManualBraille = true; var manualCells = manual.Braille;
            var manualResult = await engine.ComposeAsync(SmjFile.Snapshot(loaded).Manifest);
            if (manual.Braille != manualCells || manualResult.AlignmentUnknownBlockIds is not [1]) throw new Exception("Manual braille was changed or guessed");
            manual.HasManualBraille = false; manual.Source += "수정";
            var stale = await engine.ComposeAsync(SmjFile.Snapshot(loaded).Manifest);
            if (stale.AlignmentUnknownBlockIds is not [1]) throw new Exception("A stale source map was applied");
            manual.Source = source; manual.Translation = manual.Translation! with { LineAlignment = [new(0, 1, 0, 999, "right")] };
            var invalid = await engine.ComposeAsync(SmjFile.Snapshot(loaded).Manifest);
            if (invalid.AlignmentUnknownBlockIds is not [1]) throw new Exception("Invalid saved line alignment was trusted");
            foreach (var unusual in new[] { "가😀\n<!가운데정렬>나", "가\r\n<!오른쪽정렬>나", "가\n\n<!가운데정렬>나", "<!왼쪽정렬>가<!오른쪽정렬>나" })
            {
                var other = new EditorBlock { Id = 2, Page = 1, Source = unusual };
                var translated = await new TranslationCoordinator(engine).TranslateAsync(other, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(20));
                if (translated != TranslationOutcome.Applied || other.Translation?.LineAlignment is not null) throw new Exception("Unsafe Unicode, newlines or ambiguous tags produced a verified mapping");
                var otherModel = new DocumentSession { Layout = model.Layout }; otherModel.Add(other);
                var otherResult = await engine.ComposeAsync(SmjFile.Snapshot(otherModel).Manifest);
                if (otherResult.AlignmentUnknownBlockIds is not [2]) throw new Exception("Unsupported alignment was not reported as unverified");
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // Called for both Python and the frozen executable by IntegrationChecks.
    internal static async Task VerifyCarryAsync(LocalBrailleEngine engine)
    {
        var model = new DocumentSession { Layout = new(Columns: 8, Lines: 8, PageNumbers: "출력 안 함", Footer: "출력 안 함", ShowSourcePageNumber: false, ShowBraillePageNumber: false) };
        foreach (var block in new[] {
            new EditorBlock { Id = 1, Page = 1, Source = "<!오른쪽정렬>가\n나" },
            new EditorBlock { Id = 2, Page = 2, Source = "다" },
            new EditorBlock { Id = 3, Page = 2, Source = "<!왼쪽정렬>라\n마" } })
        {
            if (await new TranslationCoordinator(engine).TranslateAsync(block, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(20)) != TranslationOutcome.Applied)
                throw new Exception("Alignment carry fixture translation failed");
            model.Add(block);
        }
        var composed = await engine.ComposeAsync(SmjFile.Snapshot(model).Manifest);
        var actual = composed.Pages!.SelectMany(p => p.Lines).Take(5).ToArray();
        if (actual is not ["⠀⠀⠀⠀⠀⠀⠀⠫", "⠀⠀⠀⠀⠀⠀⠀⠉", "⠀⠀⠀⠀⠀⠀⠀⠊", "⠐⠣", "⠑"] || composed.AlignmentUnknownBlockIds is not { Length: 0 })
            throw new Exception("3.4.45 alignment did not carry across lines, blocks and original pages until the left tag: " + string.Join("/", actual));
        var brf = await engine.ReadBrfAsync(composed.Brf);
        if (!brf.SelectMany(p => p).Take(5).SequenceEqual(actual)) throw new Exception("Aligned preview differs from exported BRF");
        var first = model.Blocks[0]; var original = first.Braille; first.HasManualBraille = true;
        var unverified = await engine.ComposeAsync(SmjFile.Snapshot(model).Manifest);
        if (first.Braille != original || unverified.AlignmentUnknownBlockIds is not [1, 2])
            throw new Exception("Unverified alignment must flag following text until a verified reset, without editing braille");
    }
}
