using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Sandbox;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-25-c5a7h: a step-environment reader over an empty configuration.</summary>
internal static class InProcessStepEnvironmentReaders
{
    internal static InProcessStepEnvironmentReader Empty() =>
        new(new LoadedSecretValues(AgentSmithConfig.Empty()), AgentSmithConfig.Empty());
}
