using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Events;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4dc: the project half of the hub filter. Reached only for a caller who lacks a method's
/// permission globally but holds it in some project; it resolves the project the invocation's first
/// argument names and asks <see cref="ProjectAccess"/> there. A run of another project answers exactly
/// what a run that does not exist answers, and a dialog of another project what another person's dialog
/// answers, so a project-bound caller can probe neither.
/// </summary>
internal sealed class HubProjectScope(ProjectAccess access, RunProjectIndex runs)
{
    /// <summary>Whether this method's project decides, rather than today's global refusal.</summary>
    public bool Scopes(ClaimsPrincipal user, string method, IReadOnlyList<string> missing) =>
        HubMethodPermissions.SourceOf(method) != HubProjectSource.Global
        && missing.All(permission => access.HoldsAnywhere(user, permission));

    /// <summary>Why this caller may not invoke this method in the project it names, or null when it may.</summary>
    public Task<string?> RefusalAsync(HubInvocationContext invocation, ClaimsPrincipal user, RequiresPermission required)
    {
        var method = invocation.HubMethodName;
        var id = invocation.HubMethodArguments.FirstOrDefault() as string ?? string.Empty;
        return HubMethodPermissions.SourceOf(method) switch
        {
            HubProjectSource.RunArgument => RunAsync(invocation, user, required, id),
            HubProjectSource.DialogArgument => DialogAsync(invocation, user, required, id),
            _ => Task.FromResult(Anywhere(method, user, required)),
        };
    }

    private async Task<string?> RunAsync(
        HubInvocationContext invocation, ClaimsPrincipal user, RequiresPermission required, string runId)
    {
        var project = await runs.OfAsync(runId, null, invocation.Context.ConnectionAborted);
        if (string.IsNullOrEmpty(project) || !access.HoldsIn(user, [Permissions.RunsRead], project))
            return $"No run '{runId}' is visible to this caller.";
        // The caller already sees this run, so naming what else it needs there leaks nothing.
        var missing = required.Names.Where(name => !access.HoldsIn(user, [name], project)).ToList();
        return missing.Count == 0 ? null : HubPermissionFilter.Needs(invocation.HubMethodName, missing);
    }

    // A dialog grounded on no project yet — or no open conversation at all — carries nothing to scope
    // by; the method's own owner check still guards it.
    private async Task<string?> DialogAsync(
        HubInvocationContext invocation, ClaimsPrincipal user, RequiresPermission required, string dialogId)
    {
        var project = await OpenDialogProjectAsync(invocation, dialogId);
        if (string.IsNullOrEmpty(project)) return Anywhere(invocation.HubMethodName, user, required);
        return access.HoldsIn(user, required.Names, project)
            ? null
            : $"Spec dialog '{dialogId}' belongs to another principal.";
    }

    private static async Task<string?> OpenDialogProjectAsync(HubInvocationContext invocation, string dialogId)
    {
        var services = invocation.ServiceProvider;
        if (dialogId.Length == 0 || services.GetService(typeof(IUnitOfWork)) is null
            || services.GetService(typeof(ResourceProjectReader)) is not ResourceProjectReader reader) return null;
        return await reader.OpenDialogAsync(
            DispatcherDefaults.PlatformDashboard, dialogId, invocation.Context.ConnectionAborted);
    }

    private string? Anywhere(string method, ClaimsPrincipal user, RequiresPermission required)
    {
        var missing = required.Names.Where(name => !access.HoldsAnywhere(user, name)).ToList();
        return missing.Count == 0 ? null : HubPermissionFilter.Needs(method, missing);
    }
}
