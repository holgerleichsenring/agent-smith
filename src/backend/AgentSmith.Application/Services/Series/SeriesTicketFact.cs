using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: what is true of one member ticket right now, read from its runs, landing, requires edges
/// and a parked question's checkpoint. 2026-11-25-9992a: the ONE state of a series ticket — its hold (the one the ticket
/// list and page read), its pull requests by the one CI rule (<see cref="PullRequestHeadCi"/>), and the kind and words
/// <see cref="SeriesTicketState"/> reads from them. The master's tools, the dialog panel, the series page and its list render
/// this record and decide nothing of their own.</summary>
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

    public bool OpenedPullRequest { get; init; }

    /// <summary>Why the newest attempt ended, when it ended other than green.</summary>
    public string? EndDetail { get; init; }

    /// <summary>2026-11-25-9992a: per repo the open pull request, else the newest, with its CI on its head.</summary>
    public IReadOnlyList<SeriesRunPullRequestView> PullRequests { get; init; } = [];

    /// <summary>2026-11-25-9992a: the ticket's hold as the ticket list reads it; null when nothing holds it or none was read.</summary>
    public TicketHold? Hold { get; init; }

    /// <summary>2026-11-25-9992a: the keys of the series' tickets that start only once this one merged.</summary>
    public IReadOnlyList<string> NeededBy { get; init; } = [];

    /// <summary>The master question the newest attempt is parked on, with the job id its answer is delivered to.</summary>
    public DialogQuestion? PendingQuestion { get; init; }

    /// <inheritdoc cref="PendingQuestion"/>
    public string? DialogueJobId { get; init; }

    public bool Landed => LandedAt is not null;

    /// <summary>2026-11-25-9992a: the pull requests that wait for a merge — open, or opened and not read by the host yet.</summary>
    public IReadOnlyList<SeriesRunPullRequestView> Waiting => [.. PullRequests.Where(SeriesRunPullRequestView.Waits)];

    /// <summary>2026-11-25-9992a: the waiting pull requests' CI together (<see cref="PullRequestHeadCi.Of"/>); null before a read.</summary>
    public string? Ci => PullRequestHeadCi.Of(Waiting.Select(p => p.Ci));

    /// <summary>The first failed check of a red waiting pull request, with its excerpt's first line.</summary>
    public string? CiFailure => Waiting.FirstOrDefault(p => p.Ci == AgentSmith.Contracts.Reviews.CiStates.Red)?.CiFailure;

    /// <summary>One of <see cref="SeriesTicketKinds"/>.</summary>
    public string Kind => SeriesTicketState.KindOf(this);

    /// <summary>What the ticket is doing, in words — the master's table, the panel and the page's title.</summary>
    public string State => SeriesTicketState.WordsOf(this);

    /// <summary>2026-11-25-9992a: a run of the series is at work on the ticket right now — coding, queued, resuming,
    /// restarting, waiting for its model place, or refreshing its pull request's branch.</summary>
    public bool Working => Kind == SeriesTicketKinds.Running;
}
