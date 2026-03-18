using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Host.Services.Webhooks;

/// <summary>
/// Validates webhook payload signatures for GitHub, GitLab, and Azure DevOps.
/// </summary>
public static class WebhookSignatureValidator
{
    public static bool ValidateGitHub(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return true;

        if (!signatureHeader.StartsWith("sha256="))
            return false;

        var expected = signatureHeader["sha256=".Length..];
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(expected));
    }

    public static bool ValidateGitLab(string tokenHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return true;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(tokenHeader),
            Encoding.UTF8.GetBytes(secret));
    }
}
