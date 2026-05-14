using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Test-fixture resolver that returns a fixed image string for any project.
/// </summary>
internal sealed class StubAgentImageResolver(string image = "agent-smith-sandbox-agent:test") : IAgentImageResolver
{
    public string Resolve(ProjectConfig projectConfig) => image;
}
