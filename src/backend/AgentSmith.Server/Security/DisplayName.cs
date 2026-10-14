using System.Security.Claims;

namespace AgentSmith.Server.Security;

/// <summary>
/// The name a person is shown by in a conversation, read off the token's own display claims —
/// <c>name</c>, else <c>preferred_username</c>, else <c>upn</c>. It is not the installation's
/// name claim, which names a caller for attribution and defaults to the opaque <c>sub</c>; a token
/// carrying none of the three has no display name rather than a directory identifier.
/// </summary>
internal static class DisplayName
{
    private static readonly string[] DisplayClaims = ["name", "preferred_username", "upn"];

    public static string? Of(ClaimsPrincipal caller) =>
        DisplayClaims
            .Select(type => caller.FindFirst(type)?.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));
}
