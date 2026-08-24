using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0517: the one place a hub invocation is refused. It resolves the invoked method's
/// permission from <see cref="HubMethodPermissions"/> and throws when the caller does not
/// hold it — which SignalR turns into a completion carrying the error, so the connection
/// survives and only the invocation fails.
/// <para>
/// A FILTER rather than an attribute on the method: SignalR's dispatcher evaluates a hub
/// method's own <c>IAuthorizeData</c> itself, on every invocation, outside the middleware
/// pipeline and outside any switch — so an attribute would refuse the moment it landed.
/// A filter sees <c>HubMethodName</c>, reads the table, and stays silent while the switch
/// is off.
/// </para>
/// <para>
/// The switch is READ here, not modelled here: <see cref="TokenAuthorityConfig.Enforce"/>
/// is the same value the fallback policy hangs off, so a hub invocation and a route are
/// refused by one decision rather than two that can disagree.
/// </para>
/// <para>
/// Nothing here decides WHICH run a caller may watch. A permission on <c>SubscribeRun</c>
/// says the caller may watch runs — the argument is a scope, and a different phase.
/// </para>
/// </summary>
internal sealed class HubPermissionFilter(
    TokenAuthorityConfig auth, CallerIdentityResolver identities) : IHubFilter
{
    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var refusal = auth.Enforce ? Refusal(invocation) : null;
        return refusal is null ? next(invocation) : throw new HubException(refusal);
    }

    /// <summary>Why this caller may not invoke this method, or null when it may.</summary>
    private string? Refusal(HubInvocationContext invocation)
    {
        var method = invocation.HubMethodName;
        var required = HubMethodPermissions.For(method);
        if (required is null) return Unclassified(method);
        var missing = Missing(required, invocation.Context.User);
        return missing.Count == 0 ? null
            : $"'{method}' needs {string.Join(", ", missing)}, which this caller does not hold.";
    }

    private IReadOnlyList<string> Missing(RequiresPermission required, ClaimsPrincipal? caller)
    {
        var held = identities.Resolve(caller ?? new ClaimsPrincipal()).Permissions;
        return [.. required.Names.Where(name => !held.Contains(name, StringComparer.Ordinal))];
    }

    // Unreachable while the enumeration test holds, which is exactly why it fails closed:
    // a method added without a table entry costs a refused invocation, not a silent hole.
    private static string Unclassified(string method) =>
        $"'{method}' names no permission, so nobody can be authorized for it. Add it to "
        + $"{nameof(HubMethodPermissions)}.";
}
