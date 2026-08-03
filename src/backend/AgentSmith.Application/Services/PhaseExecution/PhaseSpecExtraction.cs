using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: discriminated union of phase-spec extraction outcomes for one ticket body
/// (mirrors the SpecDraftOutcome union style).
///
/// p0393 split "no spec at all" out of the error case. The gate moved into the ONE
/// code-changing preset, which runs ordinary bug and feature tickets too — and an
/// ordinary ticket carries no spec by definition. Absent is therefore a legitimate
/// state the run proceeds from; MALFORMED still fails, because that is someone
/// shipping a spec and getting it wrong, which must not degrade into "no spec".
/// </summary>
public abstract record PhaseSpecExtraction;

/// <summary>The body's single fenced yaml block validated against the phase-spec schema.</summary>
public sealed record PhaseSpecExtracted(PhaseDraft Draft) : PhaseSpecExtraction;

/// <summary>
/// No spec could be taken from the body. <see cref="IsAbsent"/> distinguishes "there was
/// none to take" (an ordinary ticket) from "there was one and it is broken".
/// </summary>
public sealed record PhaseSpecInvalid(string Error, bool IsAbsent = false) : PhaseSpecExtraction;
