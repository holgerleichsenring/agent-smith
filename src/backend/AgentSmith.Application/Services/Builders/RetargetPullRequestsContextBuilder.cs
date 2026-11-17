using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-9a39c: builder for the RetargetPullRequests step. It resolves nothing — the step reads the server's seed.</summary>
public sealed class RetargetPullRequestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new RetargetPullRequestsContext(pipeline, project);
}
