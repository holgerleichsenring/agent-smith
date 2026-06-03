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
    string? Prerequisites = null);
