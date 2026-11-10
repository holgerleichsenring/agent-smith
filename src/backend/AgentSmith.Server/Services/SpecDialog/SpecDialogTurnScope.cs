using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What one design turn runs against: the conversation's project from the config catalog, and the repositories its
/// active scope names. Carved out of <see cref="SpecDialogTurnRunner"/> by 2026-11-10-d854c, which needed the runner's
/// room for the turn's thrown and cancelled endings.
/// </summary>
public sealed class SpecDialogTurnScope(IConfigurationLoader configLoader, ServerContext serverContext)
{
    public ResolvedProject Project(string projectName)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (config.Projects.TryGetValue(projectName, out var project)) return project;
        throw new InvalidOperationException(
            $"Spec-dialog session is scoped to project '{projectName}', which is not in the config catalog.");
    }

    /// <summary>
    /// The active scope's repo names filter the project's repo set; an empty scope list means the whole project (the
    /// resolver stored all repos at session start; older rows may carry none).
    /// </summary>
    public IReadOnlyList<RepoConnection> Repos(ResolvedProject project, ActiveScope? scope)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (scope is null || scope.Repos.Count == 0) return project.Repos;
        var wanted = new HashSet<string>(scope.Repos, StringComparer.OrdinalIgnoreCase);
        var matched = project.Repos.Where(r => wanted.Contains(r.Name)).ToList();
        return matched.Count > 0 ? matched : project.Repos;
    }
}
