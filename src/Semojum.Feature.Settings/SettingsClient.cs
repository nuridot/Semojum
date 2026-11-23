using System.Text.Json;

using Semojum.Core;
using Semojum.Core.Integration;

namespace Semojum.Feature.Settings;

public sealed record AccountDefaults(BrailleOptions Options, string? FormId, string PageNumberLine);
public sealed record ProofPrice(string Code, int ExtraCredits)
{
    public override string ToString() => $"{Code switch { "MINERU" => "MinerU", "HAIKU" => "Haiku", "SONNET" => "Sonnet", "OPUS" => "Opus", _ => Code }} · 쪽당 추가 +{ExtraCredits:N0}";
}
public sealed record ProofPricing(int TextCredits, int VisualCredits, ProofPrice[] Models);
public sealed record AccountOutputOptions(string PaperSize, int LinesPerPage, int CellsPerLine, int BraillePageStart, string PageNumberLine, bool Duplex, string LineBreakUnit)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PaperSize) || PaperSize.Trim().Length > 30 || LinesPerPage is < 4 or > 100 || CellsPerLine is < 8 or > 100 ||
            BraillePageStart < 1 || PageNumberLine is not ("odd" or "every") || LineBreakUnit is not ("syllable" or "word"))
            throw new ArgumentException("용지 이름(1–30자), 줄 수(4–100), 칸 수(8–100), 시작 면(1 이상)을 확인해 주세요.");
    }
}
public sealed record BrailleForm(string FormId, string Name, bool Builtin, string? BaseFormId, PageLayout Layout, BrailleOptions Options)
{
    public override string ToString() => Name + (Builtin ? " · 기본" : " · 사용자");
}

public sealed class SettingsClient(SemojumApiClient api)
{
    public async Task<VisualOptionCatalog?> VisualOptionsAsync(CancellationToken ct)
    {
        try
        {
            var data = await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/users/visual-options", cancellationToken: ct);
            var rows = data.GetProperty("options").EnumerateArray().Select(row => new VisualChoice(
                row.GetProperty("elementType").GetString()!, row.GetProperty("option").GetInt32(), row.GetProperty("label").GetString()!));
            return new VisualOptionCatalog(rows);
        }
        catch (SemojumApiException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            return null; // Servers released before the catalog endpoint use their original settings vocabulary.
        }
    }

    public async Task<ProofPricing> ProofPricingAsync(CancellationToken ct)
    {
        var data = await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/jobs/proof-pricing", cancellationToken: ct);
        var baseRates = data.GetProperty("pageCredits");
        var models = data.GetProperty("models").EnumerateArray().Select(m => new ProofPrice(m.GetProperty("code").GetString()!, m.GetProperty("extraCredits").GetInt32())).ToArray();
        var fees = new ProofPricing(baseRates.GetProperty("PAGE_LAYOUT_TEXT").GetInt32(), baseRates.GetProperty("PAGE_LAYOUT_VISUAL").GetInt32(), models);
        if (fees.TextCredits < 0 || fees.VisualCredits < 0 || models.Length == 0 || models.Any(m => m.ExtraCredits < 0 || m.Code is not ("MINERU" or "HAIKU" or "SONNET" or "OPUS")))
            throw new System.IO.InvalidDataException("교열 모델 요금표가 올바르지 않습니다.");
        return fees;
    }
    private static AccountOutputOptions ReadOutput(JsonElement data)
    {
        var options = new AccountOutputOptions(data.GetProperty("paperSize").GetString() ?? "", data.GetProperty("linesPerPage").GetInt32(),
            data.GetProperty("cellsPerLine").GetInt32(), data.GetProperty("braillePageStart").GetInt32(), data.GetProperty("pageNumberLine").GetString() ?? "",
            data.GetProperty("duplex").GetBoolean(), data.GetProperty("lineBreakUnit").GetString() ?? "");
        options.Validate();
        return options;
    }

    public async Task<AccountOutputOptions> GetOutputAsync(CancellationToken ct = default) =>
        ReadOutput(await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/users/output-options", cancellationToken: ct));

    public async Task<AccountOutputOptions> SaveOutputAsync(AccountOutputOptions value, CancellationToken ct = default)
    {
        value.Validate();
        return ReadOutput(await api.SendAuthenticatedAsync(HttpMethod.Put, "/api/users/output-options", new {
            paperSize = value.PaperSize.Trim(), linesPerPage = value.LinesPerPage, cellsPerLine = value.CellsPerLine,
            braillePageStart = value.BraillePageStart, pageNumberLine = value.PageNumberLine, duplex = value.Duplex, lineBreakUnit = value.LineBreakUnit
        }, ct));
    }
    public async Task<AccountDefaults> DefaultsAsync(CancellationToken ct = default) => ReadDefaults(
        await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/users/braille-options", cancellationToken: ct));
    public async Task<AccountDefaults> SaveDefaultsAsync(AccountDefaults value, CancellationToken ct = default)
    {
        var body = JobClient.OptionsBody(value.Options);
        body["formId"] = value.FormId!; body["pageNumberLine"] = value.PageNumberLine;
        return ReadDefaults(await api.SendAuthenticatedAsync(HttpMethod.Put, "/api/users/braille-options", body, ct));
    }
    public async Task<BrailleForm[]> FormsAsync(CancellationToken ct = default) =>
        (await api.SendAuthenticatedAsync(HttpMethod.Get, "/api/users/braille-forms", cancellationToken: ct))
        .GetProperty("items").EnumerateArray().Select(ReadForm).ToArray();
    public async Task<BrailleForm> SaveFormAsync(string? id, string name, string? baseId, PageLayout layout, BrailleOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("양식 이름은 1–100자입니다.");
        if (id?.StartsWith("builtin:", StringComparison.Ordinal) == true) throw new ArgumentException("기본 양식은 수정할 수 없습니다.");
        layout.Validate();
        // A form lives only on the server: a value it cannot hold would be dropped or rewritten, so it is rejected up front (C-04).
        if (ServerContract.UnsupportedInForm(layout) is { Length: > 0 } unsupported)
            throw new ArgumentException("서버 양식에 저장할 수 없는 설정입니다: " + string.Join(", ", unsupported) + ". 값을 바꾸거나 작업 설정으로만 사용해 주세요.");
        var layoutBody = JobClient.LayoutBody(layout);
        layoutBody["pageNumberLine"] = ServerContract.FormPageNumberLine(layout.PageNumbers)!;
        return ReadForm(await api.SendAuthenticatedAsync(id is null ? HttpMethod.Post : HttpMethod.Patch,
            "/api/users/braille-forms" + (id is null ? "" : "/" + Uri.EscapeDataString(id)),
            new { name = name.Trim(), baseFormId = baseId, layoutOptions = layoutBody, brailleOptions = JobClient.OptionsBody(options) }, ct));
    }
    public Task DeleteFormAsync(BrailleForm form, CancellationToken ct = default)
    {
        if (form.Builtin) throw new ArgumentException("기본 양식은 삭제할 수 없습니다.");
        return api.SendAuthenticatedAsync(HttpMethod.Delete, "/api/users/braille-forms/" + Uri.EscapeDataString(form.FormId), cancellationToken: ct);
    }
    private static AccountDefaults ReadDefaults(JsonElement j) => new(JobClient.ReadOptions(j),
        j.TryGetProperty("formId", out var f) ? f.GetString() : null,
        j.TryGetProperty("pageNumberLine", out var p) ? p.GetString() ?? "odd" : "odd");
    private static BrailleForm ReadForm(JsonElement j) => new(j.GetProperty("formId").GetString()!, j.GetProperty("name").GetString()!,
        j.GetProperty("builtin").GetBoolean(), j.TryGetProperty("baseFormId", out var b) ? b.GetString() : null,
        JobClient.ReadLayout(j.GetProperty("layoutOptions")), JobClient.ReadOptions(j.GetProperty("brailleOptions")));
}
