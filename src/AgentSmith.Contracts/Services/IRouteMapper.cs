using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Maps swagger endpoints to their handler locations in source code.
/// Implementations carry framework-specific regex sets (.NET, Express, FastAPI, Spring).
/// </summary>
public interface IRouteMapper
{
    IReadOnlyList<RouteHandlerLocation> MapRoutes(
        IReadOnlyList<ApiEndpoint> endpoints,
        string repoPath);
}
