using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class PersistWorkBranchContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new PersistWorkBranchContext(
            pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos),
            pipeline.Resolved().Agent, pipeline);
}
