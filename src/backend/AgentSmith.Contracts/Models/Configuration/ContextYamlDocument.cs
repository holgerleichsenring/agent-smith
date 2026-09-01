namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Typed shape of a `.agentsmith/contexts/&lt;name&gt;/context.yaml` file (p0193).
/// The writer (WriteContextYamlToolHost) takes JSON shaped like this record
/// and emits YAML through the same YamlDotNet builder the reader uses —
/// roundtrip-safe by construction.
///
/// Meta + Stack carry strong types because the orchestrator reads them
/// (workdir + lang). Arch / Quality / Behavior are pass-through
/// dictionaries: the framework doesn't introspect them; YamlDotNet emits
/// them with the same quoting rules used everywhere else, so freeform
/// operator content survives the roundtrip without the LLM having to know
/// YAML scanner rules.
/// </summary>
public sealed record ContextYamlDocument(
    ContextYamlMeta Meta,
    ContextYamlStack? Stack = null,
    IDictionary<string, object?>? Arch = null,
    IDictionary<string, object?>? Quality = null,
    IDictionary<string, object?>? Behavior = null,
    // p0202e: optional operator OVERRIDE of the environment-prepare command,
    // read at discovery time. Default for code projects is the analyzer-derived
    // value; this is for what the analyzer can't derive (e.g. pipeline tools).
    string? Prerequisites = null,
    // p0375: templated private-registry auth files (placeholder tokens only).
    // A present section is authoritative — replayed LLM-free every run.
    ContextYamlRegistryAuth? RegistryAuth = null,
    // 2026-08-31-26d4: the ordered commands this repository declares as proof that a
    // change in it holds. Executed ahead of every other source, at this context's
    // workdir. Null = the repository declares nothing and the run infers as before.
    IReadOnlyList<ContextYamlVerifyStage>? Verify = null,
    // 2026-09-01-e14d: the files those stages were derived from — the repository's own
    // CI definition, scripts and manifests — and the framework's hash of them. The model
    // names the files; the write path stamps the hash, because a model cannot compute one.
    ContextYamlVerifyDerivation? VerifyDerivedFrom = null,
    // 2026-09-01-379a: the command that asks this context's target environment whether
    // it answers, run after the prerequisites and before the master. Null = nothing asks.
    ContextYamlTargetProbe? Probe = null);
