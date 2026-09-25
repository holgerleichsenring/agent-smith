using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: computes the difference between what a ticket SAYS and what the approved
/// specification HOLDS — one comparison, made in code, so the turn argues about a fact instead of
/// producing one.
/// <para>
/// The comparison is deliberately coarse: a phase's GOAL is one sentence, and the filed body
/// carries it verbatim under its own heading, so a goal the ticket text does not contain is a
/// goal the ticket does not state. Nothing finer would survive the conversions a description
/// makes on the way back — Azure DevOps hands back HTML, Jira a flattened document — and a
/// character-level diff of two texts a person may edit is a report nobody reads.
/// </para>
/// <para>
/// It answers null when there is nothing to put to anyone: no approved set, or a ticket that
/// already says every goal in it.
/// </para>
/// </summary>
public static partial class TicketSetDivergence
{
    /// <param name="ticketText">The ticket as the conversation holds it.</param>
    /// <param name="goals">The approved set's goals, in the order it holds them.</param>
    public static SetDivergence? Between(string? ticketText, IReadOnlyList<string> goals)
    {
        ArgumentNullException.ThrowIfNull(goals);
        if (goals.Count == 0) return null;
        var said = Normalize(ticketText);
        var unsaid = goals
            .Where(goal => !string.IsNullOrWhiteSpace(goal))
            .Where(goal => !said.Contains(Normalize(goal), StringComparison.Ordinal))
            .ToList();
        return unsaid.Count == 0 ? null : new SetDivergence(goals.Count, unsaid);
    }

    // Whitespace runs collapse and case is dropped, the way the ticket fingerprint normalises:
    // a re-flowed line is not a different sentence, and no tracker preserves the wrapping.
    private static string Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : Whitespace().Replace(text, " ").Trim().ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
