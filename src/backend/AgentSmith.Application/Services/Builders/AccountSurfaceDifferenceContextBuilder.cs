using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// 2026-08-30-c6ec: builds the surface-difference context. Everything else the step needs
/// is read on the pipeline at execution time, where an absent input becomes a stated reason
/// rather than a throw during context construction.
/// </summary>
public sealed class AccountSurfaceDifferenceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new AccountSurfaceDifferenceContext(pipeline, project.Agent);
}
