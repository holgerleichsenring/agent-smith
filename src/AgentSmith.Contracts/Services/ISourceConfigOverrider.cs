using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Applies CLI-provided source overrides to the project configuration.
/// Follows the env-var pattern: if a CLI value is set, it wins over the config file.
/// </summary>
public interface ISourceConfigOverrider
{
    void Apply(ProjectConfig project, PipelineContext pipeline);
}
