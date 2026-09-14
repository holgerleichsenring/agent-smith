using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// 2026-09-14-3f5b: what this server can SEE about its own sign-in, gathered once so
/// <see cref="SignInCheck"/> only has to decide what it means.
/// </summary>
/// <param name="Seen">
/// The callers this installation has accepted, or NULL when the store could not be read.
/// Null and empty are different answers: an unreachable database says nothing about who
/// has signed in, and reporting it as "nobody" is the most alarming thing this check can
/// say and would be false.
/// </param>
/// <param name="AdminRoutes">
/// The routes to admin that are VISIBLE from here. A directory can put the role in a claim
/// and nothing here knows until somebody arrives carrying it, so an empty list means "none
/// can be seen", never "nobody is an administrator".
/// </param>
internal sealed record SignInFacts(
    IReadOnlyList<ObservedCaller>? Seen, IReadOnlyList<string> AdminRoutes, int RetentionDays)
{
    public static async Task<SignInFacts> ReadAsync(
        RoleMappingConfig inForce,
        AdminGrant grant,
        IObservedCallerStore observed,
        CancellationToken cancellationToken)
    {
        var seen = await SeenAsync(observed, cancellationToken);
        return new SignInFacts(seen, Routes(inForce, grant, seen), inForce.ObservationRetentionDays);
    }

    /// <summary>Whether a caller has been accepted inside the retention window.</summary>
    public bool AnyAccepted => Seen is { Count: > 0 };

    /// <summary>Whether the store answered at all — the third state the count cannot carry.</summary>
    public bool WasRead => Seen is not null;

    /// <summary>The most recent acceptance, for a message that says WHEN rather than whether.</summary>
    public DateTimeOffset? LastAccepted =>
        Seen is { Count: > 0 } ? Seen.Max(c => c.LastSeen) : null;

    private static IReadOnlyList<string> Routes(
        RoleMappingConfig inForce, AdminGrant grant, IReadOnlyList<ObservedCaller>? seen)
    {
        var routes = new List<string>();
        if (inForce.PersonGrants.Any(g => Holds(g.Roles))) routes.Add("a person grant");
        if (inForce.GroupRoles.Values.Any(Holds)) routes.Add("a mapped group");
        if (grant.NamesSomebody) routes.Add(AdminGrant.EnvVar);
        if (seen?.Any(c => Holds(c.RoleValues)) == true) routes.Add("an observed role claim");
        return routes;
    }

    private static async Task<IReadOnlyList<ObservedCaller>?> SeenAsync(
        IObservedCallerStore observed, CancellationToken cancellationToken)
    {
        try
        {
            return await observed.AllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool Holds(IEnumerable<string> roles) =>
        roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase);
}
