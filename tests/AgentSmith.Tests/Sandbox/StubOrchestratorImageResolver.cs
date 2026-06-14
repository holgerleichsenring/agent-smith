using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Test-fixture resolver that returns a fixed orchestrator image for any project.
/// </summary>
internal sealed class StubOrchestratorImageResolver(string image = "agent-smith-orchestrator:test")
    : IOrchestratorImageResolver
{
    public string Resolve(ResolvedProject projectConfig) => image;
}
