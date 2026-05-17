namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Which resolution layer produced the sandbox toolchain image. Closed set so
/// the pipeline-start log line stays grep-stable across runs and dashboards.
/// Order matches the resolution priority (Override wins first, GenericFallback
/// loses last); see SandboxLanguageResolver for the actual evaluation.
/// </summary>
public enum SandboxToolchainResolutionLayer
{
    /// <summary>Operator pinned a specific image via ResolvedProject.Sandbox.ToolchainImage.</summary>
    Override,

    /// <summary>Host-side project-map.json cache from a prior AnalyzeProjectHandler run.</summary>
    HostCache,

    /// <summary>Remote .agentsmith/context.yaml read via the source provider (post-init-project).</summary>
    RemoteContextYaml,

    /// <summary>
    /// ContextKeys.ProjectMap was already populated in-memory at sandbox-create time.
    /// Never fires today (TryCreateSandboxAsync runs before any handler), kept for
    /// symmetry and for hypothetical pipelines that resolve the project map upfront.
    /// </summary>
    InMemoryProjectMap,

    /// <summary>No layer matched; falling back to the generic toolchain image.</summary>
    GenericFallback
}
