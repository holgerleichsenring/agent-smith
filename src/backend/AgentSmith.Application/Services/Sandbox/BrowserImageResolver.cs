using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283de: mirrors <see cref="AgentImageResolver"/> for the browser image. Same registry
/// (per-project override over the global default), same version decision — the two images are
/// published by one CI run under one tag, so a browser that drifted from its carrier is a pull
/// of a tag nobody built.
/// </summary>
public sealed class BrowserImageResolver(
    IOptions<SandboxGlobalConfig> globalConfig, IAgentVersionResolver versions) : IBrowserImageResolver
{
    public string Resolve(ResolvedProject projectConfig)
    {
        var registry = !string.IsNullOrEmpty(projectConfig.Sandbox?.AgentRegistry)
            ? projectConfig.Sandbox.AgentRegistry
            : globalConfig.Value.AgentRegistry;
        var version = versions.Resolve(projectConfig).Version;

        return string.IsNullOrEmpty(registry)
            ? $"{BrowserImageDefaults.SandboxBrowserImageName}:{version}"
            : $"{registry}/{BrowserImageDefaults.SandboxBrowserImageName}:{version}";
    }
}
