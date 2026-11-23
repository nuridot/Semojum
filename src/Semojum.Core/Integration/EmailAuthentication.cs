using System.Net.Http;
using System.Text.Json;

namespace Semojum.Core.Integration;

public sealed record EmailChallenge(string VerificationToken, int ExpiresInSeconds, int ResendAfterSeconds, int MaxAttempts)
{
    public override string ToString() => "EmailChallenge";
}

public sealed partial class SemojumApiClient
{
    public Task<EmailChallenge> StartSignupAsync(string email, string password, string termsVersion, string privacyVersion,
        string? marketingVersion, CancellationToken ct) => StartEmailChallengeAsync("/api/auth/email/signup",
        new { email, password, agreements = new { termsVersion, privacyVersion, marketingVersion } }, ct);

    public Task<EmailChallenge> StartPasswordResetAsync(string email, CancellationToken ct) =>
        StartEmailChallengeAsync("/api/auth/password/reset", new { email }, ct);

    public Task<EmailChallenge> ResendEmailCodeAsync(bool signup, string verificationToken, CancellationToken ct) =>
        StartEmailChallengeAsync(signup ? "/api/auth/email/signup/resend" : "/api/auth/password/reset/resend", new { verificationToken }, ct);

    private async Task<EmailChallenge> StartEmailChallengeAsync(string path, object body, CancellationToken ct)
    {
        var response = await SendOnceAsync(HttpMethod.Post, path, body, null, ct).ConfigureAwait(false);
        var challenge = response.Deserialize<EmailChallenge>(Json);
        if (challenge is not { ExpiresInSeconds: > 0, ResendAfterSeconds: >= 0, MaxAttempts: > 0 }
            || string.IsNullOrWhiteSpace(challenge.VerificationToken)) throw InvalidResponse();
        return challenge;
    }

    public async Task<AccountIdentity> VerifySignupAsync(string email, string verificationToken, string code, CancellationToken ct)
    {
        await authGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await SendOnceAsync(HttpMethod.Post, "/api/auth/email/signup/verify",
                new { verificationToken, code, keepLoggedIn = false }, null, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return AcceptLogin(email.Trim().ToLowerInvariant(), result);
        }
        finally { authGate.Release(); }
    }

    public async Task ConfirmPasswordResetAsync(string verificationToken, string code, string newPassword, CancellationToken ct) =>
        _ = await SendOnceAsync(HttpMethod.Post, "/api/auth/password/reset/confirm",
            new { verificationToken, code, newPassword }, null, ct).ConfigureAwait(false);
}
