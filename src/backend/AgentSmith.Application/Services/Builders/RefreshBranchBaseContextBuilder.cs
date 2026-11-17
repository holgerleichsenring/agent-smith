using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-023fb: the seeds a refresh-base run starts from; a missing seed reaches the handler as null.</summary>
public sealed class RefreshBranchBaseContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new RefreshBranchBaseContext(
            pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) ? repos ?? [] : [],
            Text(pipeline, ContextKeys.CheckoutBranch), Text(pipeline, ContextKeys.BaseRefreshPrUrl),
            Text(pipeline, ContextKeys.BaseRefreshPrNumber), Text(pipeline, ContextKeys.BaseRefreshBaseBranch), pipeline);

    private static string? Text(PipelineContext pipeline, string key) =>
        pipeline.TryGet<object>(key, out var value) ? value?.ToString() : null;
}
