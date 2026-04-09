using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds DraftSkillFilesContext from pipeline state.
/// </summary>
public sealed class DraftSkillFilesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var evaluations = pipeline.Get<IReadOnlyList<SkillEvaluation>>(ContextKeys.SkillEvaluations);
        return new DraftSkillFilesContext(evaluations, pipeline);
    }
}
