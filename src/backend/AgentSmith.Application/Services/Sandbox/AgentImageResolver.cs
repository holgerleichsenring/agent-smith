using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Builds the fully-qualified carrier-image reference: the compiled-in image name, the
/// registry (per-project override over the global default), and the tag
/// <see cref="IAgentVersionResolver"/> decided.
/// <para>
/// 2026-08-25-0d01 took the version question out of here. It used to be a mandatory config
/// field whose absence was an error — now it is derived from the running server unless an
/// operator names one, and that is a decision with its own reasons, its own outcome record
/// and its own reader.
/// </para>
/// </summary>
public sealed class AgentImageResolver(
    IOptions<SandboxGlobalConfig> globalConfig, IAgentVersionResolver versions) : IAgentImageResolver
{
    public string Resolve(ResolvedProject projectConfig)
    {
        var registry = FirstNonEmpty(
            projectConfig.Sandbox?.AgentRegistry, globalConfig.Value.AgentRegistry);
        var version = versions.Resolve(projectConfig).Version;

        return string.IsNullOrEmpty(registry)
            ? $"{AgentImageDefaults.SandboxAgentImageName}:{version}"
            : $"{registry}/{AgentImageDefaults.SandboxAgentImageName}:{version}";
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrEmpty(a) ? a : b;
}
