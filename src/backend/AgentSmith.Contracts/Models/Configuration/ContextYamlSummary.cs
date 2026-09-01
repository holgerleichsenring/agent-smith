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
/// <param name="Prerequisites">`prerequisites:` — operator-owned dependency-install
/// idiom (p0202a). Read here, alongside language, so it reaches the early EnsurePrerequisites
/// step. Null/absent → no install for that context.</param>
/// <param name="Image">p0265: `stack.image:` — the exact toolchain Docker image named by
/// the analyzer/context-generator LLM. Wins over the language→image convention table, so
/// any framework/version is supported without a table row. Null → fall back to the table.</param>
/// <param name="Resources">p0268: `stack.resources:` — the raw (unparsed) LLM-authored k8s
/// CPU/memory block. Carried verbatim to the SandboxResourceResolver, which is the single
/// gate that validates and either applies it or falls back loudly. Null → no per-stack size.</param>
/// <param name="Purpose">p0331: `meta.purpose:` — the human sentence describing what this
/// context is for. Surfaced so the ticket→repo scope classifier can reason about which
/// repos a ticket touches from metadata alone (pre-checkout, pre-sandbox).</param>
/// <param name="Verify">2026-08-31-26d4: `verify:` — the ordered stages this repository
/// declares as proof that a change in it holds. Read here so it reaches the verify gate
/// through the discovery, ahead of anything a model emits for that run. Null/absent → the
/// gate infers as before.</param>
/// <param name="VerifyDerivedFrom">2026-09-01-e14d: `verify_derived_from:` — the files the
/// verify block was derived from and their hash at that moment. Read here so the run can
/// re-hash them and report a declaration whose source has moved. Null/absent → nothing to
/// compare, and the run says nothing.</param>
public sealed record ContextYamlSummary(
    string Workdir, string? Language, string? Prerequisites = null, string? Image = null,
    ContextYamlStackResources? Resources = null, string? Purpose = null,
    IReadOnlyList<ContextYamlVerifyStage>? Verify = null,
    ContextYamlVerifyDerivation? VerifyDerivedFrom = null);
