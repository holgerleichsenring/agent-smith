using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// Tracks an active agent job linked to a specific chat channel.
/// Stored in Redis with TTL = 45 minutes.
/// Key: conversation:{platform}:{channelId}
/// </summary>
public sealed record ConversationState
{
    public required string JobId { get; init; }
    public required string ChannelId { get; init; }
    public required string UserId { get; init; }

    /// <summary>
    /// 2026-09-25-8e51b: the tracker connection and the spec-key spelling of the ticket this
    /// conversation belongs to, or null for one that belongs to none. A bound conversation is
    /// reachable by anyone who may reach this surface — see SpecDialogOwnership.MayReach.
    /// </summary>
    public string? Tracker { get; init; }

    /// <inheritdoc cref="Tracker"/>
    public string? TicketKey { get; init; }

    /// <summary>True when this conversation belongs to a ticket, and is therefore shared.</summary>
    public bool MayBeReachedBy(string owner) => UserId == owner || TicketKey is not null;
    public required string Platform { get; init; }
    public required string Project { get; init; }
    public required string TicketId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }

    /// <summary>
    /// The questionId currently waiting for an answer, if any.
    /// Null when no question is pending.
    /// </summary>
    public string? PendingQuestionId { get; init; }

    /// <summary>
    /// RunJob states are keyed per channel (one active job per channel).
    /// SpecDialog states are keyed per chat thread and persisted durably
    /// (p0315a) so parallel design threads stay isolated and survive restarts.
    /// </summary>
    public ConversationMode Mode { get; init; } = ConversationMode.RunJob;

    /// <summary>
    /// The chat thread this state belongs to: Slack thread_ts, Teams
    /// conversation.id. Null for channel-keyed RunJob states.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>Ordered user/assistant turns of a spec-dialog session.</summary>
    public IReadOnlyList<TranscriptTurn> Transcript { get; init; } = [];

    /// <summary>The project + repo set a spec-dialog session is scoped to.</summary>
    public ActiveScope? Scope { get; init; }

    /// <summary>
    /// 2026-09-20-4b0af: what this conversation is about, minted once from its opening
    /// exchange. Carried on the state so a later turn can see that one already exists
    /// without a second read of the row. Null until one is minted, and for every
    /// conversation that predates the mint.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// 2026-09-17-042ed: the proposal THIS turn is revising, set by the router when an edit note
    /// sends the turn round again — with the findings its review reported, which the re-prompted
    /// master is shown. Never stored: it belongs to the turn about to run, and the stored
    /// proposal is cleared only on reject or timeout, so reading it back would show a later
    /// ordinary turn stale findings.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public OutcomeProposal? Revising { get; init; }

    public ConversationState WithPendingQuestion(string questionId) =>
        this with { PendingQuestionId = questionId };

    public ConversationState ClearPendingQuestion() =>
        this with { PendingQuestionId = null };

    public ConversationState AppendTurn(TranscriptTurn turn) =>
        this with { Transcript = [.. Transcript, turn], LastActivityAt = turn.At };
}
