using AgentSmith.Domain.Models;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-f3a8a: builder for the AdminAssistantTurn step — the installation project as the run resolved it.</summary>
public sealed class AdminAssistantTurnContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new AdminAssistantTurnContext(pipeline, project);
}
