using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Semojum.Core.Integration;

namespace Semojum.Feature.Account;

public sealed record SocialAuthorization(string Code, string Verifier, string RedirectUri)
{
    public override string ToString() => nameof(SocialAuthorization);
}

public static class SocialBrowserLogin
{
    private const int RegisteredPort = 53682;

    public static async Task<SocialAuthorization> AuthorizeAsync(SocialClient client, Action<Uri> openBrowser, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool google = client.Provider == SocialProvider.Google;
        using var ipv4 = Listen(IPAddress.Loopback, google ? 0 : RegisteredPort);
        using var ipv6 = !google && Socket.OSSupportsIPv6 ? Listen(IPAddress.IPv6Loopback, RegisteredPort) : null;
        var redirect = google ? $"http://127.0.0.1:{((IPEndPoint)ipv4.LocalEndpoint).Port}" : $"http://localhost:{RegisteredPort}/callback";
        var verifier = RandomToken();
        var state = RandomToken();
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var endpoint = client.Provider switch
        {
            SocialProvider.Google => "https://accounts.google.com/o/oauth2/v2/auth",
            SocialProvider.Kakao => "https://kauth.kakao.com/oauth/authorize",
            SocialProvider.Naver => "https://nid.naver.com/oauth2/authorize",
            _ => throw new ArgumentOutOfRangeException(nameof(client))
        };
        var query = HttpUtility.ParseQueryString("");
        query["response_type"] = "code";
        query["client_id"] = client.ClientId;
        query["redirect_uri"] = redirect;
        query["state"] = state;
        query["code_challenge_method"] = "S256";
        query["code_challenge"] = challenge;
        if (google) query["scope"] = "openid email profile";
        if (client.Provider == SocialProvider.Naver) query["scope"] = "openid";
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        waiting.CancelAfter(TimeSpan.FromMinutes(5));
        openBrowser(new UriBuilder(endpoint) { Query = query.ToString() }.Uri);
        var callback = new Uri(redirect);
        var listeners = new List<Task<string>> { WaitForCodeAsync(ipv4, callback, state, waiting.Token) };
        if (ipv6 is not null) listeners.Add(WaitForCodeAsync(ipv6, callback, state, waiting.Token));
        try
        {
            var finished = await Task.WhenAny(listeners).ConfigureAwait(false);
            return new(await finished.ConfigureAwait(false), verifier, redirect);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && waiting.IsCancellationRequested)
        {
            throw new TimeoutException("브라우저 로그인 시간이 지났습니다. 다시 시작해 주세요.");
        }
        finally
        {
            waiting.Cancel();
            Task cleanup = Task.WhenAll(listeners);
            await cleanup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private static TcpListener Listen(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port) { ExclusiveAddressUse = true };
        try { listener.Start(); return listener; }
        catch { listener.Dispose(); throw; }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static async Task<string> WaitForCodeAsync(TcpListener listener, Uri callback, string state, CancellationToken ct)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            using var read = CancellationTokenSource.CreateLinkedTokenSource(ct);
            read.CancelAfter(TimeSpan.FromSeconds(2));
            using var stream = client.GetStream();
            string[] lines;
            try { lines = await ReadHeadersAsync(stream, read.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { continue; }
            catch (IOException) { continue; }
            var request = lines.FirstOrDefault()?.Split(' ');
            var host = lines.Skip(1).Where(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)).ToArray();
            bool valid = request is ["GET", var target, "HTTP/1.1" or "HTTP/1.0"] &&
                host.Length == 1 && host[0][5..].Trim().Equals(callback.Authority, StringComparison.OrdinalIgnoreCase) &&
                target.Split('?')[0] == callback.AbsolutePath;
            var query = HttpUtility.ParseQueryString(valid && request![1].Contains('?') ? request[1][(request[1].IndexOf('?') + 1)..] : "");
            bool matched = valid && query.GetValues("state") is [var received] && received == state;
            var code = matched && query.GetValues("code") is [var value] && !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 ? value : null;
            var error = matched ? query["error"] : null;
            bool accepted = matched && ((code is not null) != (error is not null));
            var body = accepted ? "인증 응답을 받았습니다. 누리닷 앱으로 돌아가 주세요." : "누리닷 로그인 요청을 확인할 수 없습니다.";
            var content = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(accepted ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\nContent-Length: {content.Length}\r\n\r\n");
            try
            {
                await stream.WriteAsync(header, ct).ConfigureAwait(false);
                await stream.WriteAsync(content, ct).ConfigureAwait(false);
            }
            catch (IOException) { /* A browser may leave as soon as it sends the redirect. */ }
            if (!accepted) continue;
            if (error == "access_denied") throw new OperationCanceledException("브라우저에서 로그인을 취소했습니다.");
            if (error is not null) throw new InvalidOperationException("인증 제공자가 로그인을 완료하지 못했습니다. 다시 시도해 주세요.");
            return code!;
        }
    }

    private static async Task<string[]> ReadHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        var bytes = new byte[16 * 1024];
        int count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), ct).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
            var end = bytes.AsSpan(0, count).IndexOf("\r\n\r\n"u8);
            if (end >= 0) return Encoding.ASCII.GetString(bytes, 0, end).Split("\r\n");
        }
        return [];
    }
}
