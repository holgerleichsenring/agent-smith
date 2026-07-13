using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>p0324: scripted config source — checks are tested without touching disk.</summary>
internal sealed class FakePreflightConfigSource(PreflightConfigResult result) : IPreflightConfigSource
{
    public static FakePreflightConfigSource Of(AgentSmithConfig config) =>
        new(new PreflightConfigResult(config, "/tmp/agentsmith.yml", null));

    public static FakePreflightConfigSource LoadFailure(string error) =>
        new(new PreflightConfigResult(null, "/tmp/agentsmith.yml", error));

    public PreflightConfigResult Resolve() => result;
}
