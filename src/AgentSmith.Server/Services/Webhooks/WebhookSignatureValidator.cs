using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Server.Services.Webhooks;

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

    public static bool ValidateAzureDevOps(string authorizationHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return true;

        // Azure DevOps service hooks use Basic Auth with username:password
        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        var encoded = authorizationHeader["Basic ".Length..];
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return false;
        }

        // The secret is sent as the password (username can be anything)
        var colonIndex = decoded.IndexOf(':');
        if (colonIndex < 0)
            return false;

        var password = decoded[(colonIndex + 1)..];
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(secret));
    }

    public static bool ValidateJira(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return true;

        // Secret configured but header missing → reject
        if (string.IsNullOrEmpty(signatureHeader))
            return false;

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
}
