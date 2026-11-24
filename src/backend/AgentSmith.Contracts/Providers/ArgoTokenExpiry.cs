using System.Text;
using System.Text.Json;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-c81ec: when an Argo CD role token expires, read from its JWT payload's 'exp' claim WITHOUT verifying it —
/// Argo CD verifies; this only says how long the token holds. A token without 'exp' (created without --expires-in) never
/// expires; a value that is not a JWT is refused naming pocs.targets[].argo.token_secret.
/// </summary>
public sealed record ArgoTokenExpiry(DateTimeOffset? ExpiresAt)
{
    /// <summary>The expiry of <paramref name="token"/>, or the reason it is no JWT.</summary>
    public static (ArgoTokenExpiry? Expiry, string? Refusal) Read(string token)
    {
        var parts = (token ?? string.Empty).Trim().Split('.');
        if (parts.Length != 3 || Payload(parts[1]) is not { } payload)
            return (null, $"{Models.Configuration.PocArgoConfig.Key}.token_secret holds no JWT (three base64url parts with a "
                + "JSON payload); store the AppProject role token 'argocd proj role create-token' printed");
        return payload.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? (new ArgoTokenExpiry(DateTimeOffset.FromUnixTimeSeconds(seconds)), null)
            : (new ArgoTokenExpiry((DateTimeOffset?)null), null);
    }

    /// <summary>The sentence the probe and the studio show: 'no expiry', the date, and a warning under
    /// <paramref name="warnDays"/>.</summary>
    public string Sentence(DateTimeOffset now, int warnDays)
    {
        if (ExpiresAt is not { } at) return "The token has no expiry.";
        if (at <= now) return $"The token expired on {at:yyyy-MM-dd}; mint a new one and store it in token_secret.";
        var days = (int)Math.Floor((at - now).TotalDays);
        return days < warnDays
            ? $"The token expires on {at:yyyy-MM-dd}, in {days} day(s) — under token_warn_days ({warnDays}); mint a new one now, "
              + "because every PoC run fails once it expires."
            : $"The token expires on {at:yyyy-MM-dd}.";
    }

    /// <summary>Whether <see cref="Sentence"/> warns.</summary>
    public bool Warns(DateTimeOffset now, int warnDays) => ExpiresAt is { } at && (at - now).TotalDays < warnDays;

    private static JsonElement? Payload(string part)
    {
        try
        {
            var padded = part.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - (padded.Length % 4)) % 4);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
