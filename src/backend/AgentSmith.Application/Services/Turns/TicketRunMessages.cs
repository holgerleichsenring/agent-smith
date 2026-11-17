using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>
/// 2026-11-17-4860b: a code run master's view of its ticket's conversation — the operator messages the conversation
/// addressed to this run, handed over with the next tool result as a design turn's are. The index of the last one
/// handed over lives in the pipeline (<see cref="ContextKeys.TicketMessagesThrough"/>), so a message reaches the run once
/// across its specs and a resume. Each call first asks only when the conversation last changed; the transcript is read
/// only when that moved since this master last looked.
/// </summary>
public sealed class TicketRunMessages(
    ITicketConversationMessages conversation, PipelineContext pipeline, string project, string ticketId, string runId)
    : IMidTurnMessages
{
    private DateTimeOffset? _seen;

    /// <summary>A code run's master leads its run; there is no steward to tell.</summary>
    public void Lead(bool stewarded) { }

    public async ValueTask<IReadOnlyList<MidTurnMessage>> DrainAsync(CancellationToken cancellationToken)
    {
        var activity = await conversation.LastActivityAsync(project, ticketId, cancellationToken);
        if (activity is null || activity == _seen) return [];
        _seen = activity;
        var waiting = await conversation.AddressedToAsync(project, ticketId, runId, Through(pipeline), cancellationToken);
        if (waiting.Count == 0) return [];
        pipeline.Set(ContextKeys.TicketMessagesThrough, waiting.Max(m => m.Index));
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.TicketMessagesHanded, // 2026-11-17-ec48a: for a spec revision's review
            [.. CheckpointedValues.Strings(pipeline, ContextKeys.TicketMessagesHanded), .. waiting.Select(m => m.Text)]);
        return [.. waiting.Select(m => new MidTurnMessage(m.Text, m.At))];
    }

    // An int as set, or the number a checkpoint's JSON round trip brings back.
    internal static int Through(PipelineContext pipeline) =>
        CheckpointedValues.Int(pipeline, ContextKeys.TicketMessagesThrough, -1);
}
