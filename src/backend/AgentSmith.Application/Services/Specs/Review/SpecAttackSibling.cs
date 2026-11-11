namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfa: another child of the epic a design turn's draft belongs to, as its reviewer reads it — so a
/// capability a sibling takes is not judged dropped from the draft. Scope in and out are the words its yaml states.
/// </summary>
public sealed record SpecAttackSibling(
    string SpecId, string Goal, IReadOnlyList<string> Repos, string? ScopeIn, string? ScopeOut);
