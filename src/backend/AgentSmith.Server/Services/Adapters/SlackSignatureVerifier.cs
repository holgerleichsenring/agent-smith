using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Verifies Slack request signatures using HMAC-SHA256.
/// Prevents replay attacks by rejecting requests older than 5 minutes.
/// See: https://api.slack.com/authentication/verifying-requests-from-slack
/// 2026-11-18-78efb: no signing secret configured skips verification, as it always did; a stored one that exists but
/// cannot be opened refuses every request, and the log names the secret and why — never the value.
/// </summary>
public sealed class SlackSignatureVerifier(CredentialLookup signingSecret, ILogger<SlackSignatureVerifier> logger)
{
    /// <summary>The verifier a Slack route uses: the configured signing secret as it resolves now.</summary>
    public static SlackSignatureVerifier For(IServiceProvider services) => new(
        services.GetRequiredService<SlackAdapterOptions>().SigningSecretLookup,
        services.GetRequiredService<ILogger<SlackSignatureVerifier>>());

    public async Task<bool> VerifyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (signingSecret.State == CredentialLookupState.Unreadable)
        {
            logger.LogError("Refusing a Slack request: {Variable} has no environment value and {Unreadable}",
                SlackAdapterOptions.SigningSecretVariable, signingSecret);
            return false;
        }
        if (signingSecret.Value is not { } secret || string.IsNullOrWhiteSpace(secret))
            return true; // skip verification in development if not configured

        if (!TryExtractHeaders(request, out var timestamp, out var signature))
            return false;

        if (IsReplayAttack(timestamp))
            return false;

        var body = await ReadBodyAsync(request, cancellationToken);
        return IsSignatureValid(secret, timestamp, body, signature);
    }

    private static bool TryExtractHeaders(
        HttpRequest request,
        out string timestamp,
        out string signature)
    {
        timestamp = string.Empty;
        signature = string.Empty;

        if (!request.Headers.TryGetValue(DispatcherDefaults.SlackTimestampHeader, out var tsValues) ||
            !request.Headers.TryGetValue(DispatcherDefaults.SlackSignatureHeader, out var sigValues))
            return false;

        timestamp = tsValues.FirstOrDefault() ?? string.Empty;
        signature = sigValues.FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(timestamp) && !string.IsNullOrWhiteSpace(signature);
    }

    private static bool IsReplayAttack(string timestamp)
    {
        if (!long.TryParse(timestamp, out var ts))
            return true;

        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts;
        return Math.Abs(age) > DispatcherDefaults.SlackReplayWindowSeconds;
    }

    private static bool IsSignatureValid(string secret, string timestamp, string body, string receivedSignature)
    {
        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var expected = $"{DispatcherDefaults.SlackSignaturePrefix}{Convert.ToHexString(hash).ToLowerInvariant()}";

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(receivedSignature));
    }

    private static async Task<string> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;
        return body;
    }
}
