namespace AgentSmith.Contracts.Models.Configuration;

/// <param name="Workdir">The sub-tree this context's SOURCE occupies, relative to the repo
/// root. "." for single-stack. REQUIRED per p0161. 2026-09-03-7bac: it places no command —
/// build, test, prerequisites and probe all run from the repository root.</param>
/// <param name="Type">`meta.type:` — one to four system archetypes (agent, pipeline, api, …).
/// 2026-08-25-056d: a LIST, because the schema has always declared it as one and both shipped
/// context files write one. As a single string the writer emitted YAML its own schema rejected,
/// and a document repeating the two archetypes this repo declares failed to deserialise at all.</param>
/// <param name="Purpose">`meta.purpose:` — what this context is FOR. The one line nobody can
/// derive: it appears in no file, and the ticket-to-repo classifier reasons from it before a
/// checkout exists.</param>
/// <remarks>
/// 2026-08-26-04b6: `project` and `version` are gone. Both were READINGS — the repository's own
/// name, which every run already carries, and a semver copied out of the build file that is
/// stale on the next release. The schema still ACCEPTS them, so no context written before is
/// invalidated; a document that still offers them is accepted and the file does not carry them.
/// 2026-08-31-77a8: `domain` joins them. It named a profile in the shared skills catalog — our
/// copy of a claim about somebody else's estate — and nothing resolves one any more.
/// </remarks>
public sealed record ContextYamlMeta(
    string Workdir,
    IReadOnlyList<string>? Type = null,
    string? Purpose = null);
