namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-11-23-31dch: who can answer the master's question — the target code's owner on its ticket, the plan's in the
/// series conversation, or the environment's admin. A question stored before the field existed is the target's.
/// </summary>
public static class QuestionAnswerers
{
    /// <summary>The ticket's own work: what the code it changes should do. Answered on the ticket.</summary>
    public const string Target = "target";

    /// <summary>The plan across tickets: a contract between tickets, their order or their scope. Answered in the series
    /// conversation; a ticket outside a series answers it on the ticket.</summary>
    public const string Plan = "plan";

    /// <summary>The environment the run works in: an unreachable repository, a missing credential, image or service.
    /// Reported to the admin.</summary>
    public const string Environment = "environment";

    public static readonly IReadOnlyList<string> All = [Target, Plan, Environment];

    /// <summary>The known answerer a value names, ignoring case and whitespace; null for a missing or unknown one.</summary>
    public static string? Parse(string? value)
    {
        var trimmed = value?.Trim();
        return All.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The one refusal of a missing or unknown answerer: it names every value and what it means, so one retry
    /// with the field set posts the question.</summary>
    public static string Refusal(string? given) =>
        (string.IsNullOrWhiteSpace(given) ? "Not posted: ask_human needs 'answerer'. "
            : $"Not posted: '{given.Trim()}' is not an answerer. ")
        + $"Set answerer to one of: '{Target}' — what the code this ticket changes should do, answered on the ticket; "
        + $"'{Plan}' — the plan across tickets (a contract between tickets, their order or scope), answered in the series "
        + $"conversation; '{Environment}' — the environment the run works in (an unreachable repository, a missing "
        + "credential, image or service), reported to the admin. Ask again with the same question and the answerer set.";
}
