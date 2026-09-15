namespace AgentSmith.Application.Models;

/// <summary>
/// p0379: outcome of the deterministic principles transfer that runs before
/// the bootstrap skill call. A non-null <paramref name="Error"/> fails the
/// round loudly — a transfer-mode round must never silently regress to
/// LLM-generated principles.
/// </summary>
public sealed record PrinciplesTransferResult(
    PrinciplesMode Mode,
    string? Error = null,
    // 2026-08-28-7675: which catalog the composition read, carried only for the mode that
    // needs explaining — principles the skill wrote because the catalog offered none.
    string? CatalogOrigin = null,
    // 2026-09-15-d66f: one outcome per enforcement artefact the delta declared. BESIDE the
    // round-level mode, not instead of it: Mode still answers what happened to principles.md
    // and still feeds the bootstrap prompt. An artefact set cannot answer that question, and
    // one preserve decision cannot answer the artefacts'.
    IReadOnlyList<ArtefactWrite>? Artefacts = null)
{
    /// <summary>Never null: a delta that declares no artefact yields none, which is an answer.</summary>
    public IReadOnlyList<ArtefactWrite> Artefacts { get; init; } = Artefacts ?? [];
}
