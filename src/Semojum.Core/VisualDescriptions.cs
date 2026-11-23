namespace Semojum.Core;

public sealed record VisualChoice(string ElementType, int Option, string Label);

public sealed class VisualOptionCatalog
{
    public IReadOnlyDictionary<string, VisualChoice[]> Choices { get; }

    public VisualOptionCatalog(IEnumerable<VisualChoice> entries)
    {
        var choices = entries.GroupBy(e => e.ElementType switch { "image" => "figure", "chart_graph" => "chart", var type => type })
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (VisualDescriptions.Choices.Keys.Any(type => !choices.TryGetValue(type, out var rows)
                || rows.Any(r => r.Option <= 0 || string.IsNullOrWhiteSpace(r.Label))
                || rows.Select(r => r.Option).Distinct().Count() != rows.Length))
            throw new System.IO.InvalidDataException("AI 설명 유형 목록이 올바르지 않습니다.");
        Choices = choices;
    }

    public static VisualOptionCatalog Legacy() => new(VisualDescriptions.Choices.SelectMany(group =>
        group.Value.Select(label => new VisualChoice(group.Key, VisualDescriptions.LegacyNumber(label, group.Key == "table")!.Value, label))));
}

public static class VisualDescriptions
{
    public static int? LegacyNumber(string choice, bool table) => table ? choice switch
    {
        "풀어쓰기" => 1, "테두리+구분선" => 2, "행열 바꿈" => 3, "테두리만" => 4, _ => null
    } : choice switch
    {
        "생략" => 1, "개조식" => 2, "별책 참조" => 6, "줄글" => 7, "상향식" => 8,
        "관행형" => 10, "짧은 제목" => 11, "상세" => 12, _ => null
    };
    public static readonly IReadOnlyDictionary<string, string[]> Choices = new Dictionary<string, string[]>
    {
        ["figure"] = ["생략", "짧은 제목", "개조식", "상세", "줄글", "별책 참조"],
        ["table"] = ["테두리+구분선", "테두리만", "행열 바꿈", "풀어쓰기"],
        ["chart"] = ["생략", "짧은 제목", "개조식", "상세", "줄글", "별책 참조"],
        ["diagram"] = ["생략", "짧은 제목", "개조식", "상세", "관행형", "상향식"],
        ["cartoon"] = ["생략", "개조식", "줄글", "별책 참조"]
    };
    public static Dictionary<string, VisualDescription> Defaults() => new()
    {
        ["figure"] = new(["짧은 제목", "개조식", "상세", "줄글"], "개조식"),
        ["table"] = new(["테두리+구분선", "테두리만", "행열 바꿈", "풀어쓰기"], "테두리+구분선"),
        ["chart"] = new(["짧은 제목", "개조식", "상세"], "개조식"),
        ["diagram"] = new(["짧은 제목", "개조식", "상세", "관행형"], "개조식"),
        ["cartoon"] = new(["개조식", "줄글"], "개조식")
    };
    public static void Validate(IReadOnlyDictionary<string, VisualDescription> values)
    {
        foreach (var (type, value) in values)
        {
            var labels = value.Options;
            bool valid = Choices.TryGetValue(type, out var choices) && labels is { Length: > 0 };
            if (valid && (value.OptionNumbers != null || value.DefaultOptionNumber != null))
            {
                var numbers = value.OptionNumbers;
                int index = numbers != null && value.DefaultOptionNumber is int number ? Array.IndexOf(numbers, number) : -1;
                valid = numbers != null && numbers.Length == labels!.Length && numbers.All(n => n > 0)
                    && numbers.Distinct().Count() == numbers.Length && labels.All(s => !string.IsNullOrWhiteSpace(s))
                    && index >= 0 && labels[index] == value.DefaultOption;
            }
            else if (valid)
                valid = labels!.Distinct().Count() == labels.Length && labels.All(choices!.Contains) && labels.Contains(value.DefaultOption);
            if (!valid) throw new ArgumentException("유형별로 하나 이상의 설명과 그중 기본 설명을 골라 주세요.");
        }
    }
}
