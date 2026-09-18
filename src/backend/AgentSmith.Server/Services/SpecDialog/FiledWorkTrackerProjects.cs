using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: the projects one filed ticket's work can be found in — every configured
/// project whose tracker connection carries the filing project's tracker NAME.
/// <para>
/// One ticket may match several projects and a run is spawned per match (ProjectResolver), so a
/// read that looked only in the filing project would miss the run that actually did the work.
/// The tracker name is the boundary: a ticket id is unique within a tracker and means nothing
/// across two.
/// </para>
/// </summary>
public sealed class FiledWorkTrackerProjects(AgentSmithConfig config)
{
    /// <summary>
    /// Every project on the filing project's tracker, the filing project included. Empty when
    /// the configuration no longer knows that project — a read that guessed would answer with
    /// another tracker's runs.
    /// </summary>
    public IReadOnlyList<FiledWorkProject> SharingTrackerWith(string project)
    {
        var filing = config.Projects
            .FirstOrDefault(p => ConfigNames.AreSame(p.Key, project)).Value;
        if (filing is null) return [];
        var tracker = filing.Tracker.Name;
        return
        [
            .. config.Projects
                // ConfigNames is the repository's ONE rule for matching a configured name;
                // an ordinal comparison here would answer differently from the catalog that
                // resolved these projects the moment two spellings differ in case.
                .Where(p => ConfigNames.AreSame(p.Value.Tracker.Name, tracker))
                .Select(p => new FiledWorkProject(p.Key, Platform(p.Value))),
        ];
    }

    /// <summary>The provider word a spec-set key is minted from, as every writer spells it.</summary>
    private static string Platform(ResolvedProject project) =>
        project.Tracker.Type.ToString().ToLowerInvariant();
}

/// <summary>One project a filed ticket's runs and spec set may live in.</summary>
public sealed record FiledWorkProject(string Name, string Platform);
