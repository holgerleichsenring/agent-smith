using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: a beat's report into the conversation — what the beat had to say, then the master's line — and
/// the table it saw, kept on the record so the next beat tells what changed. 2026-11-17-0207b: the series' opening
/// first, and the table with its one line when it changed or the status cadence is due; the operator messages the
/// beat read are read past.
/// </summary>
public sealed class PostSeriesStatusHandler(ISeriesRunStore store, ISeriesConversation conversation, AgentSmithConfig config)
    : ICommandHandler<PostSeriesStatusContext>
{
    public async Task<CommandResult> ExecuteAsync(PostSeriesStatusContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null)
            return CommandResult.Ok("No series to report.");
        var messages = SeriesReport.Messages(beat, context.Project, config.Dialogue);
        foreach (var message in messages)
            await conversation.SayAsync(beat.Record.Conversation, message, cancellationToken);
        await store.UpdateAsync(beat.Record.Id, r => SeriesReport.Seen(r, beat, messages.Count > 0), cancellationToken);
        return CommandResult.Ok($"Posted {messages.Count} message(s) into the conversation.");
    }
}
