using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;

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

    /// <summary>2026-11-23-0219: a merge asked while CI runs or has not started arms the host's auto-complete and completes once
    /// it is green — 2026-11-25-9992i: every merge into the default branch, none into a series branch.</summary>
    public bool MergeArms { get; init; }

    /// <summary>2026-11-24-f9c5c: the branch the series' tickets integrate on; null for a series without one.</summary>
    public string? IntegrationBranch { get; init; }

    /// <summary>2026-11-24-f9c5f: per repo the final pull request the series opened from its branch; empty before.</summary>
    public IReadOnlyList<SeriesRunDeliveryView> Deliveries { get; init; } = [];

    /// <summary>2026-11-25-9992i: the series' review (end, each, none) — when a person looks.</summary>
    public string Review { get; init; } = ReviewModeNames.End;

    /// <summary>2026-11-25-9992a: the keys of the tickets a run of the series is at work on now.</summary>
    public IReadOnlyList<string> WorkingOn { get; init; } = [];

    /// <summary>2026-11-25-9992j: the waiting question's choices (empty without one) and the recommended choice's index — the
    /// page draws them as the chat's buttons, and a choice sent as a message is the answer.</summary>
    public IReadOnlyList<string> QuestionChoices { get; init; } = [];

    /// <inheritdoc cref="QuestionChoices"/>
    public int? QuestionRecommended { get; init; }

    /// <summary>2026-11-25-433fa: the key of the ticket whose pull requests merge next (the list row's), null when none is
    /// ready — the chat's status line says it.</summary>
    public string? NextMerge { get; init; }

    /// <summary>2026-11-25-433fa: how far the series is, one of <see cref="SeriesProgressWord"/>'s words — merged over total.</summary>
    public string Progress { get; init; } = SeriesProgressWord.None;
}
