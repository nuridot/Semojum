namespace Semojum.Core;

public static class ReconversionPages
{
    public static int[] Parse(string input, IReadOnlySet<int> available)
    {
        if (available.Count == 0 || string.IsNullOrWhiteSpace(input)) throw new ArgumentException("다시 변환할 쪽을 입력해 주세요.");
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
            }
        }
        return numbers.ToArray();
    }
    private static ArgumentException Invalid() => new("변환을 마친 원본 쪽을 ‘3, 5–7’처럼 입력해 주세요.");
}
