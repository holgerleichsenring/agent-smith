using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>2026-11-12-92e9b: one review of a design turn's draft — the proposal with what it found, and the report
/// that goes back to the design partner before it replies; null when nothing goes back.</summary>
public sealed record DesignTurnReview(OutcomeProposal Reviewed, string? Report);
