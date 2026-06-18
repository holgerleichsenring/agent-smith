using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Optional per-project sandbox overrides. When unset, the sandbox toolchain
/// image is auto-detected (p0135): host-side project-map.json cache from a
/// prior AnalyzeProjectHandler run is consulted first, then the remote
/// .agentsmith/context.yaml via ISourceProvider.TryReadFileAsync, then the
/// in-memory ProjectMap, then a generic toolchain image.
/// </summary>
public sealed class SandboxConfig
{
    /// <summary>
    /// Override-only. When set, wins over every auto-detection layer. Use this
    /// for operators with strict base-image policies (e.g. internal mirror,
    /// pinned digests for supply-chain reasons) or for projects whose
    /// language doesn't fit the convention table (rare). For typical .NET /
    /// Node / Python / Go / Rust projects, leave unset and let auto-detection
    /// pick the matching public image.
    /// </summary>
    public string? ToolchainImage { get; set; }

    /// <summary>
    /// Per-project CPU + memory request/limit override. When null, the global
    /// SandboxOptions section (appsettings / env-vars under section "Sandbox")
    /// applies; when SandboxOptions itself is unset, <see cref="ResourceLimits.Default"/>
    /// is used. Block is all-or-nothing: partial overrides (e.g. memory_limit
    /// without cpu_limit) are not supported — pick one resolution layer.
    /// </summary>
    public ResourceLimits? Resources { get; set; }

    /// <summary>
    /// Per-project override of the sandbox agent's container registry
    /// (e.g. <c>my-corp-mirror</c>). Null = inherit the top-level <c>sandbox.agent_registry</c>
    /// from agentsmith.yml. Use this when a single project must pull the agent
    /// image from a different registry than the rest of the deployment.
    /// </summary>
    public string? AgentRegistry { get; set; }

    /// <summary>
    /// Per-project override of the sandbox agent's image tag (e.g. <c>0.49.0-beta</c>).
    /// Null = inherit the top-level <c>sandbox.agent_version</c> from agentsmith.yml.
    /// Use this to pin a specific project to a different agent build (canary, rollback).
    /// </summary>
    public string? AgentVersion { get; set; }

    /// <summary>
    /// p0230: per-project override of the per-step wall-time cap (seconds). Null =
    /// inherit the top-level <c>sandbox.step_timeout_seconds</c>. Raise it for a
    /// project whose build/test suite legitimately runs long; lower it to fail a
    /// micro-service fast.
    /// </summary>
    public int? StepTimeoutSeconds { get; set; }

    /// <summary>
    /// p0230: per-project override of the default <c>run_command</c> timeout
    /// (seconds) when the agent doesn't pass its own. Null = inherit the top-level
    /// <c>sandbox.run_command_timeout_seconds</c>. A big .NET solution wants this
    /// higher (restore+build is minutes); a tiny repo can leave it low.
    /// </summary>
    public int? RunCommandTimeoutSeconds { get; set; }

    /// <summary>
    /// p0245: per-language toolchain image overrides (language/framework slug →
    /// image), merged OVER the SandboxSpecBuilder code-default table — config wins
    /// per key. Null = inherit the code defaults. The whole-project
    /// <see cref="ToolchainImage"/> still wins outright; this is the finer-grained,
    /// polyglot-safe hook (one repo's <c>dotnet</c> can pin an internal mirror while
    /// its <c>node</c> keeps the default). Operator-declared image, no runtime
    /// install or version inference. Example: <c>{ dotnet: my-mirror/dotnet:9.0 }</c>.
    /// </summary>
    public Dictionary<string, string>? Images { get; set; }

    /// <summary>
    /// p0272: credentials to inject into the sandbox pod (env vars from a
    /// Kubernetes Secret, plus secret keys mounted as files). Null = none. The
    /// values come from operator-created k8s Secrets and never enter a Step/Redis
    /// payload or context.yaml; the consuming auth command (e.g.
    /// <c>sf org login jwt</c>) lives in context.yaml <c>prerequisites</c>.
    /// </summary>
    public SandboxSecrets? Secrets { get; set; }
}
