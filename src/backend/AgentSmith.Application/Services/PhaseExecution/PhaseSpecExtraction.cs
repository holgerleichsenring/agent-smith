using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: discriminated union of phase-spec extraction outcomes for one
/// phase-labelled ticket body (mirrors the SpecDraftOutcome union style).
/// A phase ticket without a valid spec has no absent case — the label
/// promises the block, so a missing/broken one is an error with the reason.
/// </summary>
public abstract record PhaseSpecExtraction;

/// <summary>The body's single fenced yaml block validated against the phase-spec schema.</summary>
public sealed record PhaseSpecExtracted(PhaseDraft Draft) : PhaseSpecExtraction;

/// <summary>The body carries no extractable schema-valid spec; Error names exactly why.</summary>
public sealed record PhaseSpecInvalid(string Error) : PhaseSpecExtraction;
