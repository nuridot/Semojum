using System.Text.Json;

namespace Semojum.Core.Integration;

public sealed record AppVersionInfo(Version Latest, Version Minimum, Uri? DownloadUrl, string ReleaseNotes, string? ChecksumSha256 = null)
{
    public bool Required(Version current) => current < Minimum;
    public bool Available(Version current) => current < Latest;
    public static AppVersionInfo? Read(JsonElement result, bool scopedRelease = false)
    {
        if (result.ValueKind == JsonValueKind.Null) return null;
        string? Field(string name) => result.ValueKind == JsonValueKind.Object && result.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!Version.TryParse(Field(scopedRelease ? "version" : "latestVersion"), out var latest)
            || !Version.TryParse(Field("minSupportedVersion"), out var minimum) || latest < minimum)
            throw new InvalidDataException("버전 정보가 올바르지 않습니다.");
        Uri? uri = null;
        if (result.TryGetProperty(scopedRelease ? "fileUrl" : "downloadUrl", out var download) && download.ValueKind != JsonValueKind.Null
            && (download.ValueKind != JsonValueKind.String || !Uri.TryCreate(download.GetString(), UriKind.Absolute, out uri)
                || uri.Scheme != "https" || uri.UserInfo.Length != 0))
            throw new InvalidDataException("업데이트 다운로드 주소가 올바르지 않습니다.");
        string? checksum = null;
        if (result.TryGetProperty("checksumSha256", out var hash) && hash.ValueKind != JsonValueKind.Null)
        {
            checksum = Field("checksumSha256");
            if (checksum is not { Length: 64 } || !checksum.All(Uri.IsHexDigit))
                throw new InvalidDataException("업데이트 파일의 SHA-256 값이 올바르지 않습니다.");
        }
        var notes = Field("releaseNotes");
        if (scopedRelease && result.TryGetProperty("notes", out var group) && group.ValueKind == JsonValueKind.Object
            && group.TryGetProperty("publicText", out var publicText) && publicText.ValueKind == JsonValueKind.String)
            notes = publicText.GetString();
        return new(latest, minimum, uri, notes ?? "", checksum);
    }
}
