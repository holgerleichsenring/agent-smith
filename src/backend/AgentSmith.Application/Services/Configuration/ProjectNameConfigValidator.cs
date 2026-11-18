using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-11-17-f3a8a: a project name may not start with '@'. The installation's own conversation and runs carry
/// '<see cref="InstallationScope.Name"/>' where a project name goes, so a project named like it would be read as the
/// installation — and admitted only to whoever may see the installation.
/// </summary>
public static class ProjectNameConfigValidator
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Projects.Keys.Where(name => name.StartsWith('@')).Select(name => new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            $"Project '{name}': a project name may not start with '@' — '{InstallationScope.Name}' names the "
            + "installation itself.", Project: name, Field: "projects"));
    }
}
