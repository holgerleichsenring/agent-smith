using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builds DiscoverSkillsContext from the pipeline state.
/// </summary>
public sealed class DiscoverSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillsPath = pipeline.TryGet<string>(ContextKeys.SkillsPathOverride, out var overridePath)
            ? overridePath!
            : project.SkillsPath;

        // Collect names of already-installed skills
        var installedNames = new List<string>();
        if (Directory.Exists(skillsPath))
        {
            foreach (var dir in Directory.GetDirectories(skillsPath))
            {
                installedNames.Add(Path.GetFileName(dir));
            }
        }

        return new DiscoverSkillsContext(skillsPath, installedNames.AsReadOnly(), pipeline);
    }
}
