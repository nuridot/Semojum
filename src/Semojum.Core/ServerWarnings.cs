using System.Security.Cryptography;
using System.Text;

namespace Semojum.Core;

/// <summary>
/// Actionable server quality issues are shown as open warnings over their
/// whole block. The server identifies the element. Each issue becomes a text mark and a
/// braille mark whose ids derive from the block and the issue, so the same report never adds it twice. A person's
/// edit to either text resolves the block's warnings by making them stale; the stale marks stay in the .smj so the
/// same report cannot reopen them. Issues without an element stay in the page status only.
/// </summary>
public static class ServerWarnings
{
    private const string Prefix = "server-";
    private const string TextSuffix = "-text", BrailleSuffix = "-braille";

    /// <summary>Adds open warnings for every report of <paramref name="model"/> (opening a document with a server report).</summary>
    public static int Apply(DocumentSession model) =>
        model.QualityReports.Sum(report => Apply(model.Blocks.Where(b => b.Page == report.Key), report.Value));

    /// <summary>Adds the open warnings a page's report describes, skipping any already known, open or resolved.</summary>
    public static int Apply(IEnumerable<EditorBlock> blocks, IEnumerable<QualityIssue> issues)
    {
        var added = 0;
        var byElement = issues.Where(i => i.ElementId is not null).ToLookup(i => i.ElementId);
        foreach (var block in blocks)
        {
            if (block.ServerId is not { } id) continue;
            var blockIssues = byElement[id];
            // Older SMJ files can contain generated warnings for informational entries.
            // Remove only IDs derived from those entries, preserving the user's marks.
            foreach (var info in blockIssues.Where(IsInformational))
            {
                var key = Key(id, info);
                for (var i = block.ReviewMarks.Count - 1; i >= 0; --i)
                    if (IsServerWarning(block.ReviewMarks[i]) && KeyOf(block.ReviewMarks[i].Id) == key)
                        block.ReviewMarks.RemoveAt(i);
            }
            var warnings = blockIssues.Where(i => !IsInformational(i));
            if (block.Metadata.ReviewFlag == "open" && !warnings.Any())
                warnings = [new QualityIssue("review", "review_flag", id, "안 풀린 경고")];
            foreach (var issue in warnings)
            {
                var key = Key(id, issue);
                string text = Id(key, ReviewPanel.Text), braille = Id(key, ReviewPanel.Braille);
                if (block.ReviewMarks.Any(m => m.Id == text || m.Id == braille)) continue;
                if (block.Source.Length > 0) block.ReviewMarks.Add(new(text, ReviewKind.OpenWarning, ReviewPanel.Text, 0, block.Source.Length, block.Source));
                if (block.Braille.Length > 0) block.ReviewMarks.Add(new(braille, ReviewKind.OpenWarning, ReviewPanel.Braille, 0, block.Braille.Length, block.Braille));
                if (block.Source.Length > 0 || block.Braille.Length > 0) added++;
            }
        }
        return added;
    }

    // The AI quality report includes general guidance on every matching element. Those
    // entries remain available in the report. Other R11 reasons include caption failure.
    private static bool IsInformational(QualityIssue issue) => issue.Severity == "review" &&
        (issue.Type is "R10" or "R13" or "R19" ||
         issue is { Type: "R11", Message: "AI가 쓴 시각자료 설명 — 원본 그림과 대조가 필요합니다" });

    /// <summary>A person changed the block: its open warnings are resolved and kept as stale records.</summary>
    internal static void Resolve(EditorBlock block)
    {
        for (var i = 0; i < block.ReviewMarks.Count; i++)
            if (block.ReviewMarks[i] is { Kind: ReviewKind.OpenWarning, IsStale: false } mark) block.ReviewMarks[i] = Resolved(mark);
    }

    internal static HashSet<string> OpenKeys(EditorBlock block) =>
        block.ReviewMarks.Where(m => m is { Kind: ReviewKind.OpenWarning, IsStale: false }).Select(m => KeyOf(m.Id)).OfType<string>().ToHashSet();

    /// <summary>After an automatic translation, warnings that were open cover the new cells in the braille panel.</summary>
    internal static void CoverBraille(EditorBlock block, IReadOnlySet<string> openKeys)
    {
        if (block.Braille.Length == 0) return;
        foreach (var key in openKeys)
        {
            var id = Id(key, ReviewPanel.Braille);
            var mark = new ReviewMark(id, ReviewKind.OpenWarning, ReviewPanel.Braille, 0, block.Braille.Length, block.Braille);
            var index = IndexOf(block, id);
            if (index < 0) block.ReviewMarks.Add(mark);
            else if (block.ReviewMarks[index] != mark) block.ReviewMarks[index] = mark;
        }
    }

    public static bool IsServerWarning(ReviewMark mark) => mark.Kind == ReviewKind.OpenWarning && KeyOf(mark.Id) is not null;
    /// <summary>Distinct server warnings (each has a text and a braille mark) that are open, or resolved.</summary>
    public static int Count(IEnumerable<ReviewMark> marks, bool resolved) =>
        marks.Where(m => IsServerWarning(m) && m.IsStale == resolved).Select(m => KeyOf(m.Id)).Distinct().Count();
    /// <summary>What a person's removal of a server warning leaves: the resolved record that stops it reappearing.</summary>
    public static ReviewMark Resolved(ReviewMark mark) => mark.IsStale ? mark : mark with { Start = 0, Length = 0, IsStale = true };

    private static int IndexOf(EditorBlock block, string id)
    {
        for (var i = 0; i < block.ReviewMarks.Count; i++) if (block.ReviewMarks[i].Id == id) return i;
        return -1;
    }
    // Block, severity, type and message identify the same warning; a short hash keeps the id compact in the .smj.
    private static string Key(string serverId, QualityIssue issue) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join('\u001f', serverId, issue.Severity, issue.Type, issue.Message))))[..16];
    private static string Id(string key, ReviewPanel panel) => Prefix + key + (panel == ReviewPanel.Text ? TextSuffix : BrailleSuffix);
    private static string? KeyOf(string id) =>
        !id.StartsWith(Prefix, StringComparison.Ordinal) ? null
        : id.EndsWith(TextSuffix, StringComparison.Ordinal) ? id[Prefix.Length..^TextSuffix.Length]
        : id.EndsWith(BrailleSuffix, StringComparison.Ordinal) ? id[Prefix.Length..^BrailleSuffix.Length] : null;
}
