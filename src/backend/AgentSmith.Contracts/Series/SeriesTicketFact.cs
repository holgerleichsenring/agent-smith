using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: what is true of one member ticket right now, read from its runs, landing, requires edges
/// and a parked question's checkpoint.</summary>
public sealed record SeriesTicketFact(SeriesTicket Ticket)
{
    /// <summary>The ticket ids this one requires, from its requires edges.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    public DateTimeOffset? LandedAt { get; init; }

    public DateTimeOffset? FirstStartedAt { get; init; }

    /// <summary>The summed cost of every run of the ticket.</summary>
    public decimal CostUsd { get; init; }

    public string? LatestRunId { get; init; }

    /// <summary>The newest attempt's status (running, success, failed, waiting_for_input, ...); null before any.</summary>
    public string? LatestStatus { get; init; }

    public bool LatestFinished { get; init; }

    /// <summary>2026-11-22-d434b: the newest attempt is live and waits for its model place (2026-11-20-24bd) — not stuck.</summary>
    public bool WaitingForModel { get; init; }

    public bool OpenedPullRequest { get; init; }

    /// <summary>Why the newest attempt ended, when it ended other than green.</summary>
    public string? EndDetail { get; init; }

    public IReadOnlyList<string> PullRequests { get; init; } = [];

    /// <summary>2026-11-17-0207d: the open pull requests' CI on their heads (one of CiStates: red wins, then pending,
    /// then green), as the CI loop's records hold it; null when none reports.</summary>
    public string? CiState { get; init; }

    /// <summary>The first failed check of a red CI, with its excerpt — the reason, not just the colour.</summary>
    public string? CiFailure { get; init; }

    /// <summary>The master question the newest attempt is parked on, with the job id its answer is delivered to.</summary>
    public DialogQuestion? PendingQuestion { get; init; }

    /// <inheritdoc cref="PendingQuestion"/>
    public string? DialogueJobId { get; init; }

    /// <summary>2026-11-22-6d85c: the newest attempt ended because the server stopped (or its process was lost), not on its
    /// work; <see cref="Restarting"/> while agent-smith's one automatic restart of it is still to come.</summary>
    public bool Interrupted { get; init; }

    /// <inheritdoc cref="Interrupted"/>
    public bool Restarting { get; init; }

    /// <summary>2026-11-22-6d85d: the ticket ids whose merges unblock this held ticket — every prerequisite is green and
    /// none carries the rest; empty when anything else holds it.</summary>
    public IReadOnlyList<string> StartsOnceMerged { get; init; } = [];

    public bool Landed => LandedAt is not null;
}
