using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the LoadCatalog step (p0205). The handler reads the
/// <c>CatalogResolution</c> binding and the loaded concept vocabulary from
/// <see cref="PipelineContext"/>, so no payload is needed beyond the reference.
/// </summary>
public sealed record LoadCatalogContext(PipelineContext Pipeline) : ICommandContext;
