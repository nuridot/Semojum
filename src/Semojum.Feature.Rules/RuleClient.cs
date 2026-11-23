using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

using Semojum.Core.Integration;

namespace Semojum.Feature.Rules;

public sealed record BrailleRule(string RuleId, string Source, string Section, string Name, string Contents,
    string? FullText = null, bool? Supported = null)
{
    public bool HasFullText => !string.IsNullOrWhiteSpace(FullText);
    public string ExpandedText => HasFullText ? FullText! : Contents;
    public string SupportLabel => Supported == false ? "점역 미지원 · 검색 전용" : "";
}
public sealed record RuleSearchPage(BrailleRule[] Items, int TotalPages, int TotalElements)
{
    public string[]? MatchingIds { get; init; }
}

public sealed class RuleContentsNode
{
    public string Id { get; init; } = "";
    public string? Number { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    [JsonPropertyName("rule_id")] public string? RuleId { get; init; }
    [JsonPropertyName("body_md")] public string? Markdown { get; init; }
    public bool? Translatable { get; init; }
    public List<RuleContentsNode> Children { get; init; } = [];
    [JsonIgnore] public string? LegacyText { get; init; }
    [JsonIgnore] public string Heading => string.Join(" ", new[] { Number, Title }.Where(s => !string.IsNullOrWhiteSpace(s)));
    [JsonIgnore] public string TreeLabel => string.IsNullOrWhiteSpace(Title) && RuleId is not null ? (Number + " " + Summary).Trim() : Heading;
}

public sealed class RuleClient(SemojumApiClient api)
{
    public async Task<IReadOnlyList<RuleContentsNode>> ContentsAsync(CancellationToken ct)
    {
        try
        {
            var data = await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/rules/toc", cancellationToken: ct);
            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                return data.Deserialize<RuleContentsNode[]>(JsonSerializerOptions.Web)!;
        }
        catch (SemojumApiException ex) when (ex.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            // Older servers already expose document names, section paths and plain text.
        }
        return await ContentsFromSearchAsync(ct);
    }

    private async Task<IReadOnlyList<RuleContentsNode>> ContentsFromSearchAsync(CancellationToken ct)
    {
        var roots = new List<RuleContentsNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; ; page++)
        {
            var result = ReadPage(await api.SendAuthenticatedAsync(HttpMethod.Get, $"/api/rules?page={page}&size=100", cancellationToken: ct));
            var prior = seen.Count;
            foreach (var row in result.Items)
            {
                if (string.IsNullOrWhiteSpace(row.RuleId) || !seen.Add(row.RuleId)) continue;
                var children = roots;
                var path = "";
                var sections = row.Section.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var title in new[] { row.Source }.Concat(sections.SkipLast(1)))
                {
                    path += "/" + title;
                    var branch = children.FirstOrDefault(n => n.Id == path);
                    if (branch is null) { branch = new() { Id = path, Title = title }; children.Add(branch); }
                    children = branch.Children;
                }
                children.Add(new() { Id = row.RuleId, RuleId = row.RuleId, Number = sections.LastOrDefault(),
                    Title = row.Name, Summary = row.Contents, LegacyText = row.FullText, Translatable = row.Supported });
            }
            if (page + 1 >= result.TotalPages || prior == seen.Count) break;
        }
        return roots;
    }
    public Task<RuleSearchPage> SearchAsync(string query, string? publisher, int page = 0, CancellationToken ct = default)
        => SearchPageAsync(query, publisher, page, 20, ct);

    public async Task<RuleSearchPage> SearchPageAsync(string query, string? publisher, int page, int size, CancellationToken ct, bool includeMatches = false)
    {
        if (page < 0) throw new ArgumentOutOfRangeException(nameof(page));
        if (size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(size));
        if (publisher is not (null or "" or "MCST" or "NLD" or "NISE")) throw new ArgumentException("알 수 없는 규정 문서입니다.");
        var path = $"/api/rules?page={page}&size={size}";
        if (includeMatches) path += "&includeMatches=true";
        if (!string.IsNullOrWhiteSpace(query)) path += "&q=" + Uri.EscapeDataString(query.Trim());
        if (!string.IsNullOrEmpty(publisher)) path += "&publisher=" + publisher;
        return ReadPage(await api.SendAuthenticatedAsync(HttpMethod.Get, path, cancellationToken: ct));
    }

    public static RuleSearchPage ReadPage(JsonElement data)
    {
        static string Field(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        var rows = data.GetProperty("items").EnumerateArray().Select(row => new BrailleRule(
            Field(row, "ruleId"), Field(row, "source"), Field(row, "section"), Field(row, "ruleName"), Field(row, "contents"),
            Field(row, "fullText"), row.TryGetProperty("supported", out var supported) && supported.ValueKind is JsonValueKind.True or JsonValueKind.False ? supported.GetBoolean() : null)).ToArray();
        return new(rows, data.GetProperty("totalPages").GetInt32(), data.GetProperty("totalElements").GetInt32()) {
            MatchingIds = data.TryGetProperty("matchingIds", out var ids) && ids.ValueKind == JsonValueKind.Array ? ids.Deserialize<string[]>() : null
        };
    }
}
