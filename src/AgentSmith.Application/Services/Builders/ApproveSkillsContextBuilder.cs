using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds ApproveSkillsContext from pipeline state.
/// </summary>
public sealed class ApproveSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var evaluations = pipeline.Get<IReadOnlyList<SkillEvaluation>>(ContextKeys.SkillEvaluations);
        pipeline.TryGet<string>(ContextKeys.SkillInstallPath, out var draftDir);
        return new ApproveSkillsContext(evaluations, draftDir, pipeline);
    }
}
