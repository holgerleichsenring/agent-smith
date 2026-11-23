using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: one series run as its page reads it — one model, nothing joined in the browser.</summary>
/// <param name="Conversation">The Work it out conversation's session id the series came from.</param>
/// <param name="Tickets">In depth order, then series order.</param>
/// <param name="MergeOrder">The tickets with open pull requests in the order they merge.</param>
public sealed record SeriesRunDetailView(
    string Id, string Name, string Project, string State, string Conversation, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt, SeriesRunCounts Counts, string? Question, IReadOnlyList<SeriesRunTicketView> Tickets,
    IReadOnlyList<SeriesRunMergeStep> MergeOrder);
