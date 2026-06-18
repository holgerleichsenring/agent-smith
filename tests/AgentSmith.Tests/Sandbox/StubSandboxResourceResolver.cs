using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
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
        ResolvedProject projectConfig, ContextYamlStackResources? contextResources = null) => _result;
}
