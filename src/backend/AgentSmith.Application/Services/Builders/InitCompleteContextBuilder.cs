using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// p0490: builder for InitCompleteContext. Carries the run's repos plus the work branch, so
/// the handler can resolve one ISourceProvider per repo and tell a local repository
/// which branch to fast-forward onto.
/// </summary>
public sealed class InitCompleteContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new InitCompleteContext(
            pipeline.Get<Repository>(ContextKeys.Repository).CurrentBranch,
            pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos),
            pipeline);
}
