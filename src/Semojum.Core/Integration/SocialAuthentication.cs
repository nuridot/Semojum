using System.Text.Json;

namespace Semojum.Core.Integration;

public enum SocialProvider { Google, Kakao, Naver }
public sealed record SocialClient(SocialProvider Provider, string ClientId);
public sealed record SocialSignupTicket(string Token, string? Email, string? Nickname)
{
    public override string ToString() => nameof(SocialSignupTicket);
}

public sealed class SocialLoginResult
{
    internal SocialLoginResult(AccountIdentity account) => Account = account;
    internal SocialLoginResult(SocialSignupTicket signup) => Signup = signup;
    public AccountIdentity? Account { get; }
    public SocialSignupTicket? Signup { get; }
}

public sealed partial class SemojumApiClient
{
    private static string? SocialString(JsonElement result, string name) =>
        result.ValueKind == JsonValueKind.Object && result.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() : null;

    public async Task<IReadOnlyList<SocialClient>> GetSocialProvidersAsync(CancellationToken ct)
    {
        var result = await SendOnceAsync(HttpMethod.Get, "/api/auth/social/providers", null, null, ct).ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.Array) throw InvalidResponse();
        var clients = new Dictionary<SocialProvider, SocialClient>();
        foreach (var item in result.EnumerateArray())
        {
            var provider = SocialString(item, "provider") switch
            {
                "GOOGLE" => SocialProvider.Google, "KAKAO" => SocialProvider.Kakao, "NAVER" => SocialProvider.Naver, _ => (SocialProvider?)null
            };
            if (provider is { } p && SocialString(item, "clientId") is { } id && !string.IsNullOrWhiteSpace(id))
                clients.TryAdd(p, new(p, id));
        }
        return clients.Values.OrderBy(client => client.Provider).ToArray();
    }

    public async Task<SocialLoginResult> LoginSocialAsync(SocialProvider provider, string code, string codeVerifier,
        string redirectUri, bool keepLoggedIn, CancellationToken ct)
    {
        await authGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await SendOnceAsync(HttpMethod.Post, "/api/auth/social/login/" + provider.ToString().ToLowerInvariant(),
                new { code, codeVerifier, redirectUri, keepLoggedIn }, null, ct).ConfigureAwait(false);
            if (SocialString(result, "status") == "SIGNUP_REQUIRED")
            {
                var ticket = SocialString(result, "signupToken");
                if (string.IsNullOrWhiteSpace(ticket) || !string.Equals(provider.ToString(), SocialString(result, "provider"), StringComparison.OrdinalIgnoreCase))
                    throw InvalidResponse();
                return new(new SocialSignupTicket(ticket, SocialString(result, "email"), SocialString(result, "nickname")));
            }
            return new(await AcceptSocialSessionAsync(result, ct).ConfigureAwait(false));
        }
        finally { authGate.Release(); }
    }

    public async Task<AccountIdentity> SignupSocialAsync(SocialSignupTicket ticket, string termsVersion, string privacyVersion,
        string? marketingVersion, bool keepLoggedIn, CancellationToken ct)
    {
        await authGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var agreements = new Dictionary<string, string> { ["termsVersion"] = termsVersion, ["privacyVersion"] = privacyVersion };
            if (marketingVersion is not null) agreements.Add("marketingVersion", marketingVersion);
            var result = await SendOnceAsync(HttpMethod.Post, "/api/auth/social/signup",
                new { signupToken = ticket.Token, agreements, keepLoggedIn }, null, ct).ConfigureAwait(false);
            return await AcceptSocialSessionAsync(result, ct).ConfigureAwait(false);
        }
        finally { authGate.Release(); }
    }

    private async Task<AccountIdentity> AcceptSocialSessionAsync(JsonElement result, CancellationToken ct)
    {
        var refreshToken = SocialString(result, "refreshToken");
        if (SocialString(result, "status") != "LOGGED_IN" || string.IsNullOrWhiteSpace(refreshToken) ||
            string.IsNullOrWhiteSpace(SocialString(result, "accessToken"))) throw InvalidResponse();
        try
        {
            // Social login omits loginId. Refresh returns the canonical ID needed by the credential store.
            var identity = await SendOnceAsync(HttpMethod.Post, "/api/auth/refresh", new { refreshToken }, null, ct).ConfigureAwait(false);
            var refreshed = identity.Deserialize<RefreshResult>(Json) ?? throw InvalidResponse();
            if (string.IsNullOrWhiteSpace(refreshed.LoginId) || string.IsNullOrWhiteSpace(refreshed.AccessToken) ||
                refreshed.Role is not ("ROLE_USER" or "ROLE_ADMIN" or "ROLE_ORG_ADMIN")) throw InvalidResponse();
            ct.ThrowIfCancellationRequested();
            var account = new AccountIdentity(refreshed.LoginId, refreshed.Role, refreshed.AdminScope, refreshed.Alias, refreshed.MemberType);
            return StartSession(account, refreshed.AccessToken, refreshToken);
        }
        catch
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await SendOnceAsync(HttpMethod.Post, "/api/auth/logout", new { refreshToken }, null, timeout.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is SemojumApiException or OperationCanceledException or HttpRequestException) { }
            throw;
        }
    }
}
