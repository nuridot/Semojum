using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Semojum.Core.Integration;
public sealed record PendingUpdate(string Version, Dictionary<string, string> Hashes);

/// <summary>Stages a complete portable app beside previous versions; never overwrites the running app or user documents.</summary>
public static class PortableUpdate
{
    public const string DesktopLauncher = "누리닷.exe";
    public const string EngineLauncher = "누리닷 점역 엔진.exe";
    private const string LegacyLauncher = "Semojum.Mockup.exe";
    public static string? GetPackageLauncher(IReadOnlyDictionary<string, string> hashes)
    {
        if (hashes.ContainsKey(DesktopLauncher))
            return hashes.ContainsKey("engine/" + EngineLauncher) ? DesktopLauncher : null;
        return hashes.ContainsKey(LegacyLauncher) && hashes.ContainsKey("engine/semojum-engine.exe") ? LegacyLauncher : null;
    }

    private const long MaximumBytes = 1024L * 1024 * 1024;
    public static Task<PendingUpdate> StageAsync(string zipPath, string root, Version version, CancellationToken ct = default) =>
        StageAsync(zipPath, root, version, null, ct);

    public static async Task<PendingUpdate> StageAsync(string zipPath, string root, Version version, string? checksumSha256, CancellationToken ct)
    {
        using var package = File.OpenRead(zipPath);
        if (checksumSha256 is not null)
        {
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(package, ct));
            if (!actualHash.Equals(checksumSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("다운로드한 파일의 SHA-256이 배포 정보와 일치하지 않습니다.");
            package.Position = 0;
        }
        var staging = Path.Combine(root, ".staging-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read);
            if (zip.Entries.Count > 20000) throw new InvalidDataException("업데이트 파일 수 제한을 초과했습니다.");
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.EndsWith('/')) relative = relative[..^1];
                ValidatePath(relative);
                if (!names.Add(relative) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("업데이트에 중복 경로나 링크가 있습니다.");
                var path = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
                if (entry.Length > MaximumBytes || total + entry.Length > MaximumBytes) throw new InvalidDataException("업데이트 압축 해제 용량 제한을 초과했습니다.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using var input = entry.Open();
                await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920]; int read;
                    while ((read = await input.ReadAsync(buffer, ct)) > 0) { total += read; if (total > MaximumBytes) throw new InvalidDataException("업데이트 용량 제한을 초과했습니다."); await output.WriteAsync(buffer.AsMemory(0, read), ct); }
                }
                hashes.Add(relative, await HashAsync(path, ct));
            }
            if (GetPackageLauncher(hashes) is null || !hashes.ContainsKey("Semojum.Mockup.dll") || !hashes.ContainsKey("Semojum.Mockup.runtimeconfig.json"))
                throw new InvalidDataException("누리닷 실행에 필요한 파일이 업데이트 ZIP에 없습니다.");
            var actual = AssemblyName.GetAssemblyName(Path.Combine(staging, "Semojum.Mockup.dll"));
            if (actual.Name != "Semojum.Mockup" || actual.Version?.ToString(3) != version.ToString(3)) throw new InvalidDataException("다운로드한 앱의 버전이 서버 안내와 다릅니다.");
            var destination = Path.Combine(root, version.ToString(3));
            // A completed version is immutable while a running app may use it.
            if (Directory.Exists(destination))
            {
                foreach (var pair in hashes) if (!File.Exists(Path.Combine(destination, pair.Key)) || await HashAsync(Path.Combine(destination, pair.Key), ct) != pair.Value) throw new InvalidDataException("같은 버전의 다른 파일이 이미 있습니다. 새 버전 번호로 배포해 주세요.");
            }
            else Directory.Move(staging, destination);
            var pending = new PendingUpdate(version.ToString(3), hashes);
            var temporary = Path.Combine(root, "pending-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(pending), ct); ct.ThrowIfCancellationRequested(); File.Move(temporary, Path.Combine(root, "pending.json"), true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return pending;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public static void ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 220 || path.Split('/').Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || p.Any(c => c < 32 || "<>:\"|?*\\".Contains(c))))
            throw new InvalidDataException("업데이트에 안전하지 않은 파일 경로가 있습니다.");
    }
    public static async Task<string> HashAsync(string path, CancellationToken ct = default)
    { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)); }
}
