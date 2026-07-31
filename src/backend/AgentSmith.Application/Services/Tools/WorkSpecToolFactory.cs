using AgentSmith.Application.Services;
using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0390: binds the DI-owned writer and git operations to the per-run state so
/// AgenticMasterHandler composes <c>revise_work_spec</c> with one dependency.
/// Returns null when the run derived no spec — the tool then never appears,
/// rather than appearing and answering "there is nothing to revise".
/// </summary>
public sealed class WorkSpecToolFactory(SandboxGitOperations gitOps, IWorkSpecWriter writer)
{
    public WorkSpecToolHost? Create(PipelineContext pipeline, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.Has(ContextKeys.WorkSpec)) return null;
        var repo = ResolveCarryingRepo(pipeline);
        if (repo is null) return null;
        return new WorkSpecToolHost(new WorkSpecReviser(pipeline, repo, gitOps, writer, logger));
    }

    private static RepoConnection? ResolveCarryingRepo(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos)
            || repos is not { Count: > 0 })
            return null;
        var name = pipeline.TryGet<string>(ContextKeys.WorkSpecRepo, out var n) ? n : null;
        return string.IsNullOrWhiteSpace(name)
            ? repos[0]
            : repos.FirstOrDefault(
                r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ?? repos[0];
    }
}
