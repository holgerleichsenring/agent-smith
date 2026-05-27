namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// One choice in a multiple-choice <see cref="DialogQuestion"/>. Flat record so
/// the LLM-facing tool schema (HumanToolHost.ask_human) stays primitive-typed
/// across providers. Recommendation is carried by DialogQuestion.RecommendedIndex,
/// not per-choice, for the same flatness reason.
/// </summary>
public sealed record DialogChoice(string Label, string? Description = null);
