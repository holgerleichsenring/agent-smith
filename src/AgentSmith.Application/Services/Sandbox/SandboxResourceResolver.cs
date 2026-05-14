using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Two-layer resolver: per-project SandboxConfig.Resources fully overrides the
/// global SandboxOptions defaults when set. Partial overrides are not supported —
/// callers pick one layer wholesale. See the SandboxConfig.Resources XML doc.
/// </summary>
public sealed class SandboxResourceResolver(IOptions<SandboxOptions> options) : ISandboxResourceResolver
{
    public ResourceLimits Resolve(ProjectConfig projectConfig) =>
        projectConfig.Sandbox?.Resources ?? options.Value.ToResourceLimits();
}
