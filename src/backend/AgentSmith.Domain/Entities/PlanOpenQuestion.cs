namespace AgentSmith.Domain.Entities;

/// <summary>
/// A clarification request the Plan-skill emits when status=needs_user_input.
/// Round-trip via ticket comments lands in p0128b.
/// </summary>
public sealed record PlanOpenQuestion(
    string Id,
    string Question,
    IReadOnlyList<string> Options)
{
    /// <summary>2026-11-17-0207c: the option the asker recommends, by index; a series relaying the question takes it
    /// when nobody answers by its deadline.</summary>
    public int? RecommendedIndex { get; init; }

    /// <summary>2026-11-23-31dch: who can answer it (<see cref="Models.QuestionAnswerers"/>); a question stored before the
    /// field existed is the target's.</summary>
    public string Answerer { get; init; } = Models.QuestionAnswerers.Target;
}
