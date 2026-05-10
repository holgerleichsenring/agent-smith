namespace AgentSmith.Domain.Entities;

/// <summary>
/// A clarification request the Plan-skill emits when status=needs_user_input.
/// Round-trip via ticket comments lands in p0128b.
/// </summary>
public sealed record PlanOpenQuestion(
    string Id,
    string Question,
    IReadOnlyList<string> Options);
