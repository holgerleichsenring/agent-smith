using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds EvaluateSkillsContext from pipeline state.
/// </summary>
public sealed class EvaluateSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var candidates = pipeline.Get<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates);

        var installedNames = new List<string>();
        var skillsPath = pipeline.TryGet<string>(ContextKeys.SkillsPathOverride, out var overridePath)
            ? overridePath!
            : project.SkillsPath;

        if (Directory.Exists(skillsPath))
        {
            foreach (var dir in Directory.GetDirectories(skillsPath))
            {
                installedNames.Add(Path.GetFileName(dir));
            }
        }

        return new EvaluateSkillsContext(candidates, installedNames.AsReadOnly(), pipeline);
    }
}
