namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-50bda: the link of the sandbox image chain that decided a sandbox's image,
/// most authoritative first.
/// </summary>
public enum SandboxImageLink
{
    /// <summary>sandbox.toolchain_image — the operator, whole project.</summary>
    ProjectToolchainImage,

    /// <summary>sandbox.images[lang] — the operator, per language.</summary>
    LanguageImage,

    /// <summary>context.yaml stack.image, inside the registry boundary.</summary>
    ContextImage,

    /// <summary>The language convention table.</summary>
    Catalog,

    /// <summary>The generic git-bearing image — nothing above named one.</summary>
    GenericFallback,
}
