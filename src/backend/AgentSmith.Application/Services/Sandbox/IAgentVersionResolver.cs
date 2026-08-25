using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Decides which sandbox-agent tag a project gets: the release this server is, unless an
/// operator deliberately named another one.
/// </summary>
public interface IAgentVersionResolver
{
    /// <summary>
    /// Returns the tag and how it was decided. Throws only when neither layer declares a
    /// version AND this server cannot say which release it is — the one case where there is
    /// nothing to derive from.
    /// </summary>
    AgentVersionChoice Resolve(ResolvedProject projectConfig);
}
