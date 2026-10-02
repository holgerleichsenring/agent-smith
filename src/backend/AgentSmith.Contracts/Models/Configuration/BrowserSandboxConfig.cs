using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-01-283de: the browser sandbox's own sizing, <c>sandbox.browser</c> in agentsmith.yml.
/// A Chromium render needs more than the light profile a read-only scope gets and none of the
/// build sizing a coding sandbox gets, so it has a profile of its own.
/// </summary>
public sealed class BrowserSandboxConfig
{
    /// <summary>Requests 500m / 1Gi, limits 2 CPU / 2Gi unless <c>sandbox.browser.resources</c> says otherwise.</summary>
    public ResourceLimits Resources { get; set; } = new("500m", "2000m", "1Gi", "2Gi");
}
