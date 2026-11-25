namespace Semojum.Core;

public sealed record PageReconversion(int[] Pages, string ProofModel);

public static class ReconversionPages
{
    public static int[] Parse(string input, IReadOnlySet<int> available)
    {
        if (available.Count == 0 || string.IsNullOrWhiteSpace(input)) throw new ArgumentException("재변환할 쪽을 입력해 주세요.");
        var numbers = new SortedSet<int>();
        foreach (var segment in input.Replace('–', '-').Split(','))
        {
            var ends = segment.Split('-', StringSplitOptions.TrimEntries);
            if (ends.Length is not (1 or 2) || !int.TryParse(ends[0], out int first) || first < 1) throw Invalid();
            int last = first;
            if (ends.Length == 2 && (!int.TryParse(ends[1], out last) || last < first)) throw Invalid();
            if ((long)last - first + 1 > available.Count) throw Invalid();
            for (long n = first; n <= last; n++)
            {
                if (!available.Contains((int)n)) throw Invalid();
                numbers.Add((int)n);
                if (numbers.Count > 1000) throw new ArgumentException("한 번에 최대 1,000쪽까지 재변환할 수 있습니다.");
            }
        }
        return numbers.ToArray();
    }
    private static ArgumentException Invalid() => new("재변환할 원본 쪽을 ‘3, 5–7’처럼 입력해 주세요.");
}
