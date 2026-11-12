using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps the thread a design turn ended with — and, 2026-11-12-92e9a, the thread it has grown so far — and hands it to
/// the next turn, from the database alone.
/// <para>
/// The thread applies only where the turn can fold it: compaction on with a positive trigger ratio, and a window —
/// the role's own or the dialog window. A turn with nothing new from the operator, or one whose stored start lies
/// past the transcript, renders the transcript as before. The turns the next prompt renders are those added from the
/// stored turn's start on, less digests and less the kept reply when it is the very text the master answered — a
/// substituted reply (a refused proposal's prose, a failure notice) is not what the master wrote, so it renders.
/// </para>
/// </summary>
public sealed class SpecDialogThreadStore(
    SpecDialogThreadRepository repository, DialogThreadCodec codec, IChatClientFactory chats, TimeProvider time)
{
    public async Task<DialogThread?> LoadAsync(ConversationState state, AgentConfig agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(agent);
        if (!CanFold(agent)) return null;
        var row = await repository.NewestAsync(state.JobId, ct);
        if (row is null || row.TranscriptFrom > state.Transcript.Count) return null;
        var unseen = Enumerable.Range(row.TranscriptFrom, state.Transcript.Count - row.TranscriptFrom)
            .Where(i => i != row.ReplyAt && state.Transcript[i].Kind != SpecDialogTurnKind.Digest)
            .Select(i => state.Transcript[i]).ToList();
        if (!unseen.Any(t => t.Role == TranscriptRole.User)) return null;
        return new DialogThread(row.Stewarded, codec.Decode(row.MessagesJson), SpecDialogTurnSeeds.MapTranscript(unseen));
    }

    /// <summary>Stores the turn's thread; nothing when the turn captured none. <paramref name="kept"/> is the state
    /// the reply was appended into, whose last turn is that reply.</summary>
    public Task KeepAsync(ConversationState started, SpecDialogTurnResult result, ConversationState? kept, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Thread?.Messages is not { } messages) return Task.CompletedTask;
        var answered = kept is not null && string.Equals(result.Reply, result.Thread.AnsweredText, StringComparison.Ordinal);
        return WriteAsync(started, messages, result.Stewarded, answered ? kept!.Transcript.Count - 1 : null, ct);
    }

    /// <summary>2026-11-12-92e9a: stores the thread of a turn still running, which has no reply yet.</summary>
    public Task KeepGrowingAsync(
        ConversationState started, IReadOnlyList<ChatMessage> messages, bool stewarded, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(messages);
        return WriteAsync(started, messages, stewarded, null, ct);
    }

    /// <summary>Whether a turn under <paramref name="agent"/> can fold a replayed thread, which Load requires.</summary>
    public bool CanFold(AgentConfig agent) =>
        agent.Compaction is { IsEnabled: true, MaxContextTokensTriggerRatio: > 0 }
        && (chats.GetContextWindowTokens(agent, TaskType.Primary) ?? agent.DialogContextWindowTokens) > 0;

    private Task WriteAsync(
        ConversationState started, IReadOnlyList<ChatMessage> messages, bool stewarded, int? replyAt, CancellationToken ct) =>
        repository.ReplaceAsync(new SpecDialogThread
        {
            SessionId = started.JobId, Stewarded = stewarded, MessagesJson = codec.Encode(messages),
            TranscriptFrom = started.Transcript.Count, ReplyAt = replyAt, WrittenAt = time.GetUtcNow(),
        }, ct);
}
