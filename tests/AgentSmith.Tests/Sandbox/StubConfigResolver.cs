using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Test-fixture config resolver returning fixed, non-throwing effective values
/// for any project. Used where a unit under test needs an IConfigResolver but
/// does not assert on the resolved values themselves.
/// </summary>
internal sealed class StubConfigResolver : IConfigResolver
{
    public ResolvedProjectSettings Resolve(ResolvedProject project) => new(
        project.Name,
        ResolvedValue<int>.Global(900),
        ResolvedValue<int>.Global(300),
        ResolvedValue<ResourceLimits>.Global(ResourceLimits.Default),
        ResolvedValue<string>.Global("agent-smith-sandbox-agent:test"),
        ResolvedValue<string>.Global("agent-smith-orchestrator:test"),
        ResolvedValue<string>.PerRun(),
        ResolveCostCap(project.Pipeline));

    public ResolvedValue<int> ResolveStepTimeout(ResolvedProject project) =>
        ResolvedValue<int>.Global(900);

    public ResolvedValue<int> ResolveRunCommandTimeout(ResolvedProject project) =>
        ResolvedValue<int>.Global(300);

    public ResolvedValue<CostCapValues> ResolveCostCap(string? pipelineName) =>
        ResolvedValue<CostCapValues>.Global(new CostCapValues());

    public ResolvedConfig Materialize() =>
        new(new Dictionary<string, ResolvedProjectSettings>());
}
