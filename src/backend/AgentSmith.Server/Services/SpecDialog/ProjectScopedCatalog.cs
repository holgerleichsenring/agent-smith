using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-15-4a4dd: the configuration ticket search, resolve and pick are handed — every project for a global
/// dialog.write holder, else only the projects the caller may write a design conversation in and the trackers those
/// projects name, so no tracker only another project routes to is even asked. A tracker an admitted project shares
/// is asked: its hits are tracker tickets the admitted project may take on, not another project's records.
/// </summary>
internal sealed class ProjectScopedCatalog(CallerProjectScope scopes)
{
    public AgentSmithConfig Narrow(AgentSmithConfig config, ClaimsPrincipal user)
    {
        var scope = scopes.For(user, Permissions.DialogWrite, config.Projects.Keys);
        if (scope.Only is null) return config;
        var projects = config.Projects.Where(p => scope.Admits(p.Key))
            .ToDictionary(p => p.Key, p => p.Value, ConfigNames.Comparer);
        return new AgentSmithConfig
        {
            Projects = projects,
            Trackers = config.Trackers
                .Where(t => projects.Values.Any(p => ConfigNames.AreSame(p.Tracker.Name, t.Value.Name)))
                .ToDictionary(t => t.Key, t => t.Value),
        };
    }
}
