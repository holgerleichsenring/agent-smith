using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class RunReviewPhaseContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new PhaseAdvanceContext(PipelinePhase.Review, Round: 2, pipeline.Resolved().Agent, pipeline);
}

public sealed class RunFinalPhaseContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new PhaseAdvanceContext(PipelinePhase.Final, Round: 3, pipeline.Resolved().Agent, pipeline);
}
