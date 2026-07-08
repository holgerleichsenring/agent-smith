using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0315b: tier-1 spec-dialog grounding. Publishes ContextKeys.CodeMap from
/// the CACHED ProjectMaps of the run's repos (IProjectMapStore prefix read —
/// no sandbox, no analyzer, staleness accepted by design). A repo with no
/// cached map is reported inline so the master knows that grounding tier is
/// absent and escalates to the read-only source sandbox instead of guessing.
/// </summary>
public sealed class LoadCachedCodeMapHandler(
    IProjectMapStore mapStore,
    ILogger<LoadCachedCodeMapHandler> logger)
    : ICommandHandler<LoadCachedCodeMapContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadCachedCodeMapContext context, CancellationToken cancellationToken)
    {
        var repos = context.Pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var sections = new StringBuilder();
        var hits = 0;
        foreach (var repo in repos)
        {
            var maps = await mapStore.ListByPrefixAsync(repo.Name, cancellationToken);
            AppendRepoSection(sections, repo.Name, maps);
            hits += maps.Count;
            if (maps.Count == 0)
                logger.LogInformation(
                    "No cached code map for repo '{Repo}' — content questions fall back to the source sandbox",
                    repo.Name);
        }

        context.Pipeline.Set(ContextKeys.CodeMap, sections.ToString().TrimEnd());
        return CommandResult.Ok($"Cached code map: {hits} map(s) across {repos.Count} repo(s)");
    }

    private static void AppendRepoSection(
        StringBuilder sections, string repoName, IReadOnlyList<ProjectMap> maps)
    {
        sections.AppendLine($"### {repoName}");
        if (maps.Count == 0)
        {
            sections.AppendLine(
                "(no cached code map — read the source through your tools when this repo matters)");
        }
        else
        {
            foreach (var map in maps)
                sections.AppendLine(ProjectMapTextRenderer.ToCodeMapText(map));
        }
        sections.AppendLine();
    }
}
