namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0161d: one component surfaced by the read-only BootstrapDiscover round.
/// A component is independently deployable or independently callable, proved
/// by an entrypoint or deploy artefact. A library without either is NOT a
/// component, whether it ships to a registry or the solution consumes it
/// internally — a project named for the layer it holds is an internal library
/// until an entrypoint or a deploy artefact of its own proves otherwise.
///
/// Produced by BootstrapDiscoverHandler from the LLM's structured response;
/// consumed by BootstrapDispatchHandler to fan out one BootstrapRound per
/// (repo, component) — each round writes
/// <c>.agentsmith/contexts/&lt;Name&gt;/{context.yaml,principles.md}</c>.
/// </summary>
/// <param name="Name">Context-directory slug under <c>.agentsmith/contexts/</c>. Operator-readable, lowercase, no slashes.</param>
/// <param name="Workdir">Repo-relative sub-tree this component's SOURCE occupies (e.g. "server", "client"); "." only where that source is the whole repo.</param>
/// <param name="Language">Free-form language slug (the LLM owns the vocabulary; see p0155). Drives bootstrap-skill activation via the <c>project_language</c> concept.</param>
/// <param name="Evidence">The file path the LLM cited as proof this component is deployable or callable (entrypoint or deploy artefact).</param>
public sealed record DiscoveredComponent(
    string Name,
    string Workdir,
    string Language,
    string Evidence);
