using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: the key of a person's PoC project, 'poc-&lt;slug&gt;', from a READABLE claim — the configured name
/// claim when it is readable (not the opaque sub), else preferred_username, else the e-mail's local part — lower-cased
/// and folded to a DNS label. With no readable claim the slug is 'u' plus 8 hex of the name-claim value's sha256. A key
/// that belongs to someone else takes '-&lt;4 hex&gt;' of the same hash, so the same person always gets the same key.
/// </summary>
internal static partial class PocProjectName
{
    private const int MaxSlugChars = 54; // not a limit: keeps the key a DNS label, RFC 1035, with room for the suffix
    private const int AnonymousHexChars = 8; // not a limit: the opaque slug's width
    private const int SuffixHexChars = 4; // not a limit: the suffix's width

    /// <summary>The value the caller's PersonRef carries: the configured name claim's value.</summary>
    public static string NameValue(ClaimsPrincipal user) => user.Identity?.Name ?? SpecDialogOwnership.PrincipalOf(user);

    public static string Base(ClaimsPrincipal user, string nameClaim)
    {
        ArgumentNullException.ThrowIfNull(user);
        var slug = Slug(Readable(user, nameClaim));
        return IPocProjects.Prefix + (slug.Length > 0 ? slug : "u" + Hash(NameValue(user))[..AnonymousHexChars]);
    }

    public static string Suffixed(string key, ClaimsPrincipal user) =>
        $"{key}-{Hash(NameValue(user))[..SuffixHexChars]}";

    private static string? Readable(ClaimsPrincipal user, string nameClaim)
    {
        if (ReadableName.Is(nameClaim) && user.Identity?.Name is { Length: > 0 } name) return name;
        if (user.FindFirst("preferred_username")?.Value is { Length: > 0 } preferred) return preferred;
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
        return email is { Length: > 0 } && email.IndexOf('@') is var at and > 0 ? email[..at] : null;
    }

    private static string Slug(string? value)
    {
        if (value is null) return string.Empty;
        var folded = NotLabel().Replace(value.ToLowerInvariant(), "-").Trim('-');
        return folded.Length <= MaxSlugChars ? folded : folded[..MaxSlugChars].TrimEnd('-');
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotLabel();
}
