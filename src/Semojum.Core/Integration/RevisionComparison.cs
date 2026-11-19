using System.Text;

namespace Semojum.Core.Integration;

public sealed record RevisionComparison(string ComparisonStatus, string Algorithm, int? BaselineLength, int? FinalLength,
    int? Insertions, int? Deletions, int? Substitutions, int? EditDistance)
{
    public const string AlgorithmName = "levenshtein_codepoint_nfc_v1";
    public static RevisionComparison Compare(string? baseline, string final)
    {
        if (baseline is null) return Missing("baseline_missing");
        try
        {
            static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);
            var a = Normalize(baseline).EnumerateRunes().ToArray(); var b = Normalize(final).EnumerateRunes().ToArray();
            if (a.SequenceEqual(b)) return new("comparable", AlgorithmName, a.Length, b.Length, 0, 0, 0, 0);
            if (a.Length == 0 || b.Length == 0) return new("comparable", AlgorithmName, a.Length, b.Length, b.Length, a.Length, 0, a.Length + b.Length);
            // Bounded memory/CPU. Large uncomparable blocks are explicit missing observations, never approximate edit distances.
            if ((long)(a.Length + 1) * (b.Length + 1) > 4_000_000) return Missing("unsupported");
            var costs = new int[a.Length + 1, b.Length + 1];
            for (var i = 0; i <= a.Length; i++) costs[i, 0] = i;
            for (var j = 0; j <= b.Length; j++) costs[0, j] = j;
            for (var i = 1; i <= a.Length; i++) for (var j = 1; j <= b.Length; j++)
                costs[i, j] = Math.Min(costs[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1), Math.Min(costs[i - 1, j] + 1, costs[i, j - 1] + 1));
            var x = a.Length; var y = b.Length; var insertions = 0; var deletions = 0; var substitutions = 0;
            while (x > 0 || y > 0)
            {
                // Contract tie order: diagonal, deletion, insertion.
                if (x > 0 && y > 0 && costs[x, y] == costs[x - 1, y - 1] + (a[x - 1] == b[y - 1] ? 0 : 1))
                { if (a[x - 1] != b[y - 1]) substitutions++; x--; y--; }
                else if (x > 0 && costs[x, y] == costs[x - 1, y] + 1) { deletions++; x--; }
                else { insertions++; y--; }
            }
            return new("comparable", AlgorithmName, a.Length, b.Length, insertions, deletions, substitutions, costs[a.Length, b.Length]);
        }
        catch (ArgumentException) { return Missing("unsupported"); }
    }
    private static RevisionComparison Missing(string status) => new(status, AlgorithmName, null, null, null, null, null, null);
}
