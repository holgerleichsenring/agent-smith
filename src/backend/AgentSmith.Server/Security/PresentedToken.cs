using Microsoft.IdentityModel.JsonWebTokens;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-09-14-c72e: the three fields of a REFUSED token that say which shape it was, read
/// from the token its bearer presented on this same request.
/// <para>
/// The failure event cannot produce them. <c>SecurityTokenInvalidAudienceException</c>
/// carries the audience and <c>SecurityTokenInvalidIssuerException</c> the issuer, but
/// <see cref="RefusedToken"/> unwraps an aggregate to its FIRST inner exception, so one
/// request yields one exception and therefore one value — and no exception carries a token
/// version at all. The live incident this was written for is exactly that case: an Entra v1
/// access token refused on its audience, with the issuer half of the same mismatch
/// invisible. So the token is read once more, here, without being validated.
/// </para>
/// <para>
/// UNVERIFIED, AND THEREFORE DISPLAY ONLY. These values are what the caller SAYS they sent;
/// the signature over them failed or was never checked. Nothing may decide anything on
/// them — they are rendered beside what the server expected, for the person holding the
/// token, and that is the whole of their use.
/// </para>
/// </summary>
internal sealed record PresentedToken(string? Audience, string? Issuer, string? Version)
{
    private const string BearerPrefix = "Bearer ";
    private const string VersionClaim = "ver";

    /// <summary>
    /// What the bearer on this request carried, or null when there is none or it cannot be
    /// read. A token that does not decode is not an error here: the refusal is reported
    /// without it, which is the malformed case saying what it already says.
    /// </summary>
    public static PresentedToken? Read(HttpContext context)
    {
        var raw = Bearer(context);
        if (raw is null) return null;
        try
        {
            var token = new JsonWebToken(raw);
            return new PresentedToken(
                token.Audiences.FirstOrDefault(),
                string.IsNullOrEmpty(token.Issuer) ? null : token.Issuer,
                token.TryGetPayloadValue<string>(VersionClaim, out var version) ? version : null);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    private static string? Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            && header.Length > BearerPrefix.Length
                ? header[BearerPrefix.Length..].Trim()
                : null;
    }
}
