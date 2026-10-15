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
/// 2026-10-15-4a4dc: WHICH run or dialog is decided for a caller who holds a missing permission
/// only through a project binding — <see cref="HubProjectScope"/> resolves the invocation's project
/// and asks there. Every other caller (enforce off, a global hold, no binding carrying what is
/// missing) meets exactly the filter it met before, with no lookup.
/// </para>
/// </summary>
internal sealed class HubPermissionFilter(
    TokenAuthorityConfig auth, CallerIdentityResolver identities, HubProjectScope scope) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var refusal = auth.Enforce ? await RefusalAsync(invocation) : null;
        return refusal is null ? await next(invocation) : throw new HubException(refusal);
    }

    /// <summary>The refusal naming what a caller lacks for a method.</summary>
    internal static string Needs(string method, IEnumerable<string> missing) =>
        $"'{method}' needs {string.Join(", ", missing)}, which this caller does not hold.";

    /// <summary>Why this caller may not invoke this method, or null when it may.</summary>
    private async Task<string?> RefusalAsync(HubInvocationContext invocation)
    {
        var method = invocation.HubMethodName;
        var required = HubMethodPermissions.For(method);
        if (required is null) return Unclassified(method);
        var caller = invocation.Context.User ?? new ClaimsPrincipal();
        var missing = Missing(required, caller);
        if (missing.Count == 0) return null;
        return scope.Scopes(caller, method, missing)
            ? await scope.RefusalAsync(invocation, caller, required)
            : Needs(method, missing);
    }

    private IReadOnlyList<string> Missing(RequiresPermission required, ClaimsPrincipal caller)
    {
        var held = identities.Resolve(caller).Permissions;
        return [.. required.Names.Where(name => !held.Contains(name, StringComparer.Ordinal))];
    }

    // Unreachable while the enumeration test holds, which is exactly why it fails closed:
    // a method added without a table entry costs a refused invocation, not a silent hole.
    private static string Unclassified(string method) =>
        $"'{method}' names no permission, so nobody can be authorized for it. Add it to "
        + $"{nameof(HubMethodPermissions)}.";
}
