using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: what a round's cuts made of one spec. <see cref="Revised"/> is the spec the next round
/// reviews — the input draft itself when the revision was refused or failed, and <see cref="Refusal"/> then
/// names why; null when the revision was admitted.
/// </summary>
public sealed record SpecCutRevision(SpecDraft Revised, string? Refusal);
