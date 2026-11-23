using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: one series run as its page reads it — one model, nothing joined in the browser.</summary>
/// <param name="Conversation">The Work it out conversation's session id the series came from.</param>
/// <param name="Tickets">In depth order, then series order.</param>
/// <param name="MergeOrder">The tickets with open pull requests in the order they merge.</param>
public sealed record SeriesRunDetailView(
    string Id, string Name, string Project, string State, string Conversation, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt, SeriesRunCounts Counts, string? Question, IReadOnlyList<SeriesRunTicketView> Tickets,
    IReadOnlyList<SeriesRunMergeStep> MergeOrder)
{
    /// <summary>2026-11-23-31dcd: the repos its tickets have pull requests in, in the project's repo order.</summary>
    public IReadOnlyList<string> Repos { get; init; } = [];

    /// <summary>2026-11-23-31dcd: every run of its tickets plus the master's own loops.</summary>
    public decimal CostUsd { get; init; }

    /// <summary>2026-11-23-31dcd: the conversation's dashboard thread (its dialog id); null when the session is gone.</summary>
    public string? ThreadId { get; init; }

    /// <summary>2026-11-23-31dcd: whether the conversation is open on that thread; a closed one is resumed in Work it out.</summary>
    public bool ConversationOpen { get; init; }

    /// <summary>2026-11-23-31dcf: every requires edge between two of its tickets, with what it means for the branches.</summary>
    public IReadOnlyList<SeriesRunEdgeView> Edges { get; init; } = [];
}
