using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0315b: tier-1 spec-dialog grounding. Publishes ContextKeys.RepoCodeMaps
/// (p0384: the only code-map surface, one entry per repo) from the CACHED
/// ProjectMaps of the run's repos (IProjectMapStore prefix read — no sandbox,
/// no analyzer, staleness accepted by design). A repo with no cached map is
/// reported inline so the master knows that grounding tier is absent and
/// escalates to the read-only source sandbox instead of guessing.
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
        var perRepo = new Dictionary<string, string>(StringComparer.Ordinal);
        var hits = 0;
        foreach (var repo in repos)
        {
            var maps = await mapStore.ListByPrefixAsync(repo.Name, cancellationToken);
            perRepo[repo.Name] = RenderRepoSection(maps);
            hits += maps.Count;
            if (maps.Count == 0)
                logger.LogInformation(
                    "No cached code map for repo '{Repo}' — content questions fall back to the source sandbox",
                    repo.Name);
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepoCodeMaps, perRepo);
        return CommandResult.Ok($"Cached code map: {hits} map(s) across {repos.Count} repo(s)");
    }

    private static string RenderRepoSection(IReadOnlyList<ProjectMap> maps)
    {
        if (maps.Count == 0)
            return "(no cached code map — read the source through your tools when this repo matters)";
        var section = new StringBuilder();
        foreach (var map in maps)
            section.AppendLine(ProjectMapTextRenderer.ToCodeMapText(map));
        return section.ToString().TrimEnd();
    }
}
