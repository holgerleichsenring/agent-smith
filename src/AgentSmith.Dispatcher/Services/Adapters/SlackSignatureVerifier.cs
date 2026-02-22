using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Dispatcher.Services.Adapters;

/// <summary>
/// Verifies Slack request signatures using HMAC-SHA256.
/// Prevents replay attacks by rejecting requests older than 5 minutes.
/// See: https://api.slack.com/authentication/verifying-requests-from-slack
/// </summary>
public sealed class SlackSignatureVerifier(string signingSecret)
{
    public async Task<bool> VerifyAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signingSecret))
            return true; // skip verification in development if not configured

        if (!TryExtractHeaders(request, out var timestamp, out var signature))
            return false;

        if (IsReplayAttack(timestamp))
            return false;

        var body = await ReadBodyAsync(request, cancellationToken);
        return IsSignatureValid(timestamp, body, signature);
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

    private bool IsSignatureValid(string timestamp, string body, string receivedSignature)
    {
        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
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
