using AgentSmith.Application.Services.Series;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dcg: the stored facts a ticket's hold is read from, gathered by the list and the detail alike — the newest
/// attempt, a parked question or hand-back, the restart that takes an interruption up, the series' merge order, the merges a
/// never-run ticket starts after, and a person's edit of its text still open in the conversation.
/// </summary>
public sealed record TicketHoldFacts(string Project, string TicketId)
{
    /// <summary>Done or closed: nothing holds a finished ticket.</summary>
    public bool Finished { get; init; }

    /// <summary>The newest attempt's status; null for a ticket never run.</summary>
    public string? HeadStatus { get; init; }

    public string? HeadRunId { get; init; }

    /// <summary>The newest attempt's number, from 1.</summary>
    public int? Attempt { get; init; }

    public string? EndKind { get; init; }

    public bool EndedOnInfrastructure { get; init; }

    /// <summary>What the newest attempt's stop recorded about its cause; null when it recorded nothing.</summary>
    public AgentSmith.Domain.Models.EndDetail? EndDetail { get; init; }

    /// <summary>The dashboard path of the setting the end detail names; null when it names none.</summary>
    public string? SettingsPath => EndDetail?.SettingsPath;

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>The newest attempt waits for a person: parked with no place in the queue.</summary>
    public bool Parked { get; init; }

    /// <summary>A pending question holds the parked attempt; without one its work was handed back.</summary>
    public bool Asked { get; init; }

    /// <summary>2026-11-23-31dch: who can answer the pending question (<see cref="AgentSmith.Domain.Models.QuestionAnswerers"/>);
    /// empty without one, the target's for a question stored before the field existed.</summary>
    public IReadOnlyList<string> Answerers { get; init; } = [];

    /// <summary>When the question was asked or the work handed back.</summary>
    public DateTimeOffset? ParkedAt { get; init; }

    /// <summary>The newest attempt ended because its process was lost, not its work (<c>OwedWorkCancel</c>).</summary>
    public bool Interrupted { get; init; }

    /// <summary>The interrupted restart (2026-11-22-6d85c) takes that attempt up.</summary>
    public bool Restarting { get; init; }

    /// <summary>The newest series run that lists the ticket; null outside any.</summary>
    public string? SeriesId { get; init; }

    /// <summary>2026-11-23-31dch: that series run is still active, so its conversation reads a plan question's answer.</summary>
    public bool SeriesActive { get; init; }

    /// <summary>The ticket's place in that series' merge order; null when no pull request of it waits there.</summary>
    public SeriesRunMergeStep? MergeStep { get; init; }

    /// <summary>2026-11-23-d60d: the ticket's open pull requests; null when none is open.</summary>
    public TicketOpenPullRequests? OpenPullRequests { get; init; }

    /// <summary>A never-run ticket starts once these are merged (2026-11-22-6d85d).</summary>
    public IReadOnlyList<string> StartsOnceMerged { get; init; } = [];

    /// <summary>A never-run ticket's unlanded requirements that no merge alone lifts.</summary>
    public IReadOnlyList<string> WaitsFor { get; init; } = [];

    /// <summary>A person edited the ticket's text and the conversation has not answered it (2026-11-22-a720b).</summary>
    public bool TextEdited { get; init; }

    /// <summary>2026-11-23-31dci: since when the running attempt waits for its model place; null when it does not.</summary>
    public DateTimeOffset? WaitingForModelSince { get; init; }

    /// <summary>2026-11-23-31dci: since when the attempt waits in the capacity queue; null outside it.</summary>
    public DateTimeOffset? EnqueuedAt { get; init; }

    /// <summary>2026-11-23-31dci: since when a never-run ticket waits for its requirements — its specs' approval.</summary>
    public DateTimeOffset? FiledAt { get; init; }

    /// <summary>2026-11-23-31dci: the minutes a wait on nobody may last before it is the admin's; 0 never.</summary>
    public int EscalateAfterMinutes { get; init; }

    /// <summary>2026-11-23-31dci: the time the hold is read at — a wait's age is counted to it; null counts none.</summary>
    public DateTimeOffset? Now { get; init; }
}
