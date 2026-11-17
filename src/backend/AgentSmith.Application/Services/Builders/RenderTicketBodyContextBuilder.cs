using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-dd05b: builder for the RenderTicketBody step — the run's project with the run's repositories.</summary>
public sealed class RenderTicketBodyContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new RenderTicketBodyContext(pipeline, RunProject.Of(project, pipeline));
}
