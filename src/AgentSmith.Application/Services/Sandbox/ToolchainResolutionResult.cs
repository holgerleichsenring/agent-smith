namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Outcome of SandboxLanguageResolver.ResolveAsync — the detected language
/// (or null when no layer matched) and which layer produced it. The Override
/// layer is owned by PipelineExecutor.TryCreateSandboxAsync (it's image-level,
/// not language-level) and never appears in a resolver result.
/// </summary>
public sealed record ToolchainResolutionResult(
    string? Language,
    SandboxToolchainResolutionLayer Layer);
