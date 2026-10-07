using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0315d: builds the WritePhaseRecord context from the checked-out repository.
/// 2026-10-06-03c7e: and the repositories the carrier is resolved against — the run's, else the
/// project's, as the derivation resolves them.</summary>
public sealed class WritePhaseRecordContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pipeline);
        var repository = pipeline.Get<Repository>(ContextKeys.Repository);
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : project.Repos;
        return new WritePhaseRecordContext(repository, pipeline, repos);
    }
}
