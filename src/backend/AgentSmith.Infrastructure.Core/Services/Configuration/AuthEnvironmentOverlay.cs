using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0503b: lays the three AGENTSMITH_AUTH_* environment variables over whatever the config
/// file declared. A Kubernetes operator reaches for environment variables — the authority
/// and audience of a cluster's identity provider are per-environment values, and a mounted
/// ConfigMap is the wrong place to vary them — so the environment wins, per field.
/// <para>
/// Environment alone is enough: an installation with no <c>auth:</c> key in its file still
/// gets an auth block if any of the three variables is set. That is also what makes an
/// enforce switch set without an authority visible as a block that is present and unusable
/// rather than as silence.
/// </para>
/// </summary>
public sealed class AuthEnvironmentOverlay
{
    public const string AuthorityVar = "AGENTSMITH_AUTH_AUTHORITY";
    public const string AudienceVar = "AGENTSMITH_AUTH_AUDIENCE";
    public const string EnforceVar = "AGENTSMITH_AUTH_ENFORCE";

    // p0503d: the three claim NAMES a directory decides. They vary per identity provider
    // rather than per installation, so they belong beside the authority they arrive with.
    public const string RoleClaimVar = "AGENTSMITH_AUTH_ROLE_CLAIM";
    public const string GroupClaimVar = "AGENTSMITH_AUTH_GROUP_CLAIM";
    public const string NameClaimVar = "AGENTSMITH_AUTH_NAME_CLAIM";

    public TokenAuthorityConfig? Apply(TokenAuthorityConfig? declared)
    {
        var authority = Read(AuthorityVar);
        var audience = Read(AudienceVar);
        var enforce = Read(EnforceVar);
        var claims = new[] { Read(RoleClaimVar), Read(GroupClaimVar), Read(NameClaimVar) };
        if (declared is null && authority is null && audience is null && enforce is null
            && claims.All(claim => claim is null)) return null;

        return new TokenAuthorityConfig
        {
            Authority = authority ?? declared?.Authority,
            Audience = audience ?? declared?.Audience,
            Enforce = enforce is null ? declared?.Enforce ?? false : IsTrue(enforce),
            RoleClaim = claims[0] ?? declared?.RoleClaim ?? Defaults.RoleClaim,
            GroupClaim = claims[1] ?? declared?.GroupClaim ?? Defaults.GroupClaim,
            NameClaim = claims[2] ?? declared?.NameClaim ?? Defaults.NameClaim,
            // p0503d: the group mapping and the custom roles are STRUCTURE, not a scalar
            // a cluster varies per environment, so they stay in the file the overlay
            // lays over and are carried through unchanged.
            GroupRoles = declared?.GroupRoles ?? [],
            Roles = declared?.Roles ?? [],
        };
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static readonly TokenAuthorityConfig Defaults = new();
}
