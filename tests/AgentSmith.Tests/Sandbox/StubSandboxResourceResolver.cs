using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Test-fixture resolver that returns <see cref="ResourceLimits.Default"/> for
/// any project. Callers that want to assert specific resource handling pass
/// an explicit <see cref="ResourceLimits"/> via the constructor.
/// </summary>
internal sealed class StubSandboxResourceResolver(ResourceLimits? fixedResult = null) : ISandboxResourceResolver
{
    private readonly ResourceLimits _result = fixedResult ?? ResourceLimits.Default;

    public ResourceLimits Resolve(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null) => _result;

    /// <summary>The fixed result stands for the global default, so that is the layer it
    /// reports — a stub that named a layer its value did not come from would be the one
    /// thing the layer accessor exists to prevent.</summary>
    public SandboxResourceLayer ResolveLayer(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null) => SandboxResourceLayer.GlobalDefault;
}
