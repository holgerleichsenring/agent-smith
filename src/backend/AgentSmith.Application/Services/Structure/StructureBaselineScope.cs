using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565c: the context a baseline is taken for — its meta files, its workdir, the
/// normalized workdirs of the other components of the same repository, whose files are theirs,
/// and what the transfer did with principles.md. 2026-10-13-2d0c: the old Limits a refresh
/// compares with are the baseline's own record, no longer the principles text the round started from.
/// </summary>
public sealed record StructureBaselineScope(
    string ContextName, string Workdir, IReadOnlyList<string> SiblingWorkdirs, PrinciplesMode Mode)
{
    public string PrinciplesPath => $"{ProjectMetaPaths.MetaDirFor(ContextName)}/{ProjectMetaPaths.PrinciplesFile}";

    public string BaselinePath => $"{ProjectMetaPaths.MetaDirFor(ContextName)}/{StructureBaselineFile.FileName}";

    public static StructureBaselineScope For(
        PipelineContext pipeline, BootstrapRoundContext round, PrinciplesMode mode)
    {
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents, out var byRepo);
        var siblings = (byRepo?.GetValueOrDefault(round.RepoName) ?? [])
            .Where(c => c.Name != round.ContextName)
            .Select(c => StructureWorkdir.Normalize(c.Workdir)).OfType<string>().ToList();
        return new StructureBaselineScope(round.ContextName, round.Workdir, siblings, mode);
    }
}
