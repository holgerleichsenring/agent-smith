namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Minimal projection of a context.yaml shape: the fields the orchestrator
/// needs before doing real work (workdir for sandbox clone path, language for
/// toolchain image). Produced by IContextYamlParser; used by both pre-sandbox
/// discovery (SandboxLanguageResolver via ISourceProvider) and post-sandbox
/// discovery (ProjectMetaResolver via ISandboxFileReader) so both layers
/// agree on what "summary" means.
/// </summary>
/// <param name="Workdir">`meta.workdir:` — sub-tree relative to repo root. "." for single-stack.</param>
/// <param name="Language">`stack.lang:` — null if absent (generic-image fallback).</param>
/// <param name="InstallCommand">`ci.install_command:` — operator-owned dependency-install
/// idiom (p0202a). Read here, alongside language, so it reaches the early InstallDependencies
/// step. Null/absent → no install for that context.</param>
public sealed record ContextYamlSummary(string Workdir, string? Language, string? InstallCommand = null);
