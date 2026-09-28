namespace AgentSmith.Contracts.Models;

/// <summary>
/// A run a person started from a chat thread, and the thread it reports back to. Written when
/// the run is launched and closed when its outcome was posted, so a run that waits days for an
/// answer, outlives the replica that started it, or outlives the conversation state still
/// finds its way back to the thread.
/// <para>
/// <see cref="ReplyEndpoint"/> is the address a platform needs beyond channel and thread to
/// post into the conversation (the Teams service URL); null where the platform has one
/// address. <see cref="QuestionId"/> and <see cref="QuestionJson"/> hold the last question of
/// the run that was posted to the thread — the one an answer from the thread belongs to.
/// </para>
/// </summary>
public sealed record ChatRunBindingFact(
    string RunId,
    string Platform,
    string ChannelId,
    string? ThreadId,
    string RequestedBy,
    string? ReplyEndpoint,
    DateTimeOffset BoundAt,
    string? QuestionId = null,
    string? QuestionJson = null);
