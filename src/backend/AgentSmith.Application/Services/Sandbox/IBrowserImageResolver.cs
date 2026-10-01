using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283de: the fully-qualified reference of the browser image a render sandbox runs —
/// published beside the carrier, so it is pulled from the carrier's registry at the carrier's tag.
/// </summary>
public interface IBrowserImageResolver
{
    /// <summary>Returns "{registry}/agent-smith-sandbox-browser:{version}".</summary>
    string Resolve(ResolvedProject projectConfig);
}
