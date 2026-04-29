using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds InstallSkillsContext from pipeline state.
/// </summary>
public sealed class InstallSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var approved = pipeline.Get<IReadOnlyList<SkillEvaluation>>(ContextKeys.ApprovedSkills);
        var draftDir = pipeline.Get<string>(ContextKeys.SkillInstallPath);
        var installPath = pipeline.Resolved().SkillsPath;
        return new InstallSkillsContext(approved, draftDir, installPath, pipeline);
    }
}
