using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-5701b: builder for the PlanFiling step. It resolves nothing — the step reads the request the
/// server seeded and fails with a message when there is none.</summary>
public sealed class PlanFilingContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new PlanFilingContext(pipeline, project);
}
