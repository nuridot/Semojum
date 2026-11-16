using Semojum.Core;

internal static class TranslationCancellationChecks
{
    private sealed class Engine : ITranslationEngine
    {
        public TaskCompletionSource Started { get; } = new();
        public bool Cancelled { get; private set; }
        public async Task<string> TranslateAsync(string text, int blockId, CancellationToken cancellationToken)
        {
            if (text == "first")
            {
                Started.SetResult();
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { Cancelled = true; throw; }
            }
            return "⠁";
        }
    }
    internal static async Task RunAsync()
    {
        var engine = new Engine(); var coordinator = new TranslationCoordinator(engine);
        var block = new EditorBlock { Id = 1, Source = "first" };
        var old = coordinator.TranslateAsync(block, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(5));
        await engine.Started.Task;
        block.Source = "latest";
        var latest = await coordinator.TranslateAsync(block, ManualEditDecision.Overwrite, TimeSpan.FromSeconds(5));
        var previous = await old;
        if (!engine.Cancelled || previous != TranslationOutcome.Superseded || latest != TranslationOutcome.Applied
            || block.Translation?.SourceText != "latest" || block.IsBrailleLocked)
            throw new Exception("The superseded request was not cancelled or overwrote the latest result.");
    }
}
