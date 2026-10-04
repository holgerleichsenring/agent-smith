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
    IReadOnlyList<ArtefactWrite>? Artefacts = null,
    // 2026-10-03-cf20c: the framework overlays composed into principles.md — set only when the
    // file was WRITTEN. A preserved file carries none of them, so naming one would ask the
    // operator to ratify rules that are not in the file.
    IReadOnlyList<string>? Overlays = null,
    // 2026-10-04-2bf2: a refresh found a Project Specifics section in the file it replaced and
    // copied it into the new one. False on a refresh says the operator had nothing to keep.
    bool ProjectSpecificsKept = false)
{
    /// <summary>Never null: no overlay is an answer.</summary>
    public IReadOnlyList<string> Overlays { get; init; } = Overlays ?? [];

    /// <summary>Never null: a delta that declares no artefact yields none, which is an answer.</summary>
    public IReadOnlyList<ArtefactWrite> Artefacts { get; init; } = Artefacts ?? [];
}
