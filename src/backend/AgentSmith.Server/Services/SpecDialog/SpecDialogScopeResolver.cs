using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Resolves the active scope (project + repo set) for a new spec-dialog
/// session from the connection/project catalog: an explicit pick wins, a
/// single configured project is the default, anything else needs a choice.
/// </summary>
public sealed class SpecDialogScopeResolver(IConfigurationLoader configLoader)
{
    public ScopeResolution Resolve(string? requestedProject)
    {
        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
        var projects = config.Projects;

        if (!string.IsNullOrWhiteSpace(requestedProject))
        {
            return projects.TryGetValue(requestedProject, out var picked)
                ? new ScopeResolved(ToScope(requestedProject, picked.Repos.Select(r => r.Name)))
                : new ScopeUnknownProject(requestedProject, [.. projects.Keys]);
        }

        if (projects.Count == 1)
        {
            var only = projects.First();
            return new ScopeResolved(ToScope(only.Key, only.Value.Repos.Select(r => r.Name)));
        }

        return new ScopeChoiceRequired([.. projects.Keys]);
    }

    private static ActiveScope ToScope(string project, IEnumerable<string> repos) =>
        new() { Project = project, Repos = [.. repos] };
}
