namespace Semojum.Core;

/// <summary>Select and present description drafts without deleting original alternatives.</summary>
public static class VisualDraftSelection
{
    private static int? OptionNumber(string choice, bool table) => table ? choice switch
    {
        "풀어쓰기" => 1, "테두리+구분선" => 2, "행열 바꿈" => 3, "테두리만" => 4, _ => null
    } : choice switch
    {
        "생략" => 1, "개조식" => 2, "별책 참조" => 6, "줄글" => 7, "상향식" => 8,
        "관행형" => 10, "짧은 제목" => 11, "상세" => 12, _ => null
    };

    public static bool IsEnabled(BlockMetadata metadata, BrailleOptions options, AlternativeDraft draft, string? renderMode = null)
    {
        // Older documents without an explicit policy retain all their supplied drafts.
        if (options.VisualDescriptions is null) return true;
        var type = metadata.VisualSubtype is { } subtype && VisualDescriptions.Choices.ContainsKey(subtype) ? subtype : metadata.Type switch
        { "image" => "figure", "graph" or "chart_graph" => "chart", var other => other };
        if (!options.VisualDescriptions.TryGetValue(type, out var policy)) return true;
        if (type == "table" && renderMode is not null)
        {
            var label = renderMode switch { "table_grid" => "테두리+구분선", "linear" => "테두리만", "transposed" => "행열 바꿈", "unfold" => "풀어쓰기", _ => null };
            return label is null || policy.Options.Contains(label);
        }
        var name = draft.Label.Trim();
        bool table = type == "table";
        VisualDescriptions.Choices.TryGetValue(type, out var choices);
        if (!table && draft.Option == 2 && policy.Options.Any(choice => (choice is "짧은 제목" or "상세") &&
                metadata.Alternatives?.Any(d => d.Option == OptionNumber(choice, false)) != true))
            return true; // Older servers supplied only a general description.
        if (draft.Option is { } number && choices?.Any(choice => OptionNumber(choice, table) == number) == true)
            return policy.Options.Any(choice => OptionNumber(choice, table) == number);
        if (choices?.Contains(name) == true) return policy.Options.Contains(name);
        return name is not ("설명" or "기본") || policy.Options.Any(o => o is "짧은 제목" or "개조식" or "상세" or "줄글");
    }
    public static bool ApplyInitial(EditorBlock block, BrailleOptions options)
    {
        if (block.HasManualBraille || block.Metadata.IsBlocked) return false;
        var metadata = block.Metadata;
        var type = metadata.VisualSubtype is { } subtype && VisualDescriptions.Choices.ContainsKey(subtype) ? subtype : metadata.Type switch
        { "image" => "figure", "graph" or "chart_graph" => "chart", var other => other };
        var policies = options.VisualDescriptions ?? VisualDescriptions.Defaults();
        if (!policies.TryGetValue(type, out var policy)) return false;
        var choice = policy.DefaultOption;
        if (metadata.Type == "table")
        {
            var mode = choice switch { "테두리+구분선" => "table_grid", "테두리만" => "linear", "행열 바꿈" => "transposed", "풀어쓰기" => "unfold", _ => null };
            if (mode is not null) block.Metadata = metadata = metadata with { RenderMode = mode };
        }
        var drafts = metadata.Alternatives ?? [];
        int? option = OptionNumber(choice, type == "table");
        var selected = option is { } number ? Array.FindIndex(drafts, d => d.Option == number && IsEnabled(metadata, options, d)) : -1;
        if (selected < 0) selected = Array.FindIndex(drafts, d => d.Label.Trim() == choice);
        if (selected < 0 && choice == "생략") selected = Array.FindIndex(drafts, d => d.Label.Trim().EndsWith("생략", StringComparison.Ordinal));
        if (selected < 0 && choice == "별책 참조") selected = Array.FindIndex(drafts, d => d.Label.Contains("참조", StringComparison.Ordinal));
        if (selected < 0 && choice is "짧은 제목" or "상세")
            selected = Array.FindIndex(drafts, d => d.Option == 2 && IsEnabled(metadata, options, d));
        if (selected < 0 && choice is "짧은 제목" or "개조식" or "상세" or "줄글")
            selected = Array.FindIndex(drafts, d => d.Label.Trim() is "설명" or "기본");
        if (selected < 0) return metadata.Type == "table" && metadata.RenderMode is not null;
        block.Metadata = metadata with { SelectedIndex = selected, OriginalSource = metadata.OriginalSource ?? block.Source };
        block.Source = drafts[selected].TaggedText ?? drafts[selected].Text;
        block.Translation = null;
        return true;
    }
}
