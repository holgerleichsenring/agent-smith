using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: discriminated union of outcome-resolution results for one
/// design-partner reply (mirrors the SpecDraftOutcome union style).
/// </summary>
public abstract record OutcomeResolution;

/// <summary>The reply resolved to a typed, validated outcome proposal.</summary>
public sealed record OutcomeResolved(OutcomeProposal Proposal) : OutcomeResolution;

/// <summary>The reply's terminal outcome failed validation; Error names exactly what.</summary>
public sealed record OutcomeInvalid(string Error) : OutcomeResolution;
