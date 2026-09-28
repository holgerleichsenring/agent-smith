using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// One pass over the open chat-run bindings, read from the database — which is why a replica
/// that just started, or one that never saw the launch, follows every bound run all the same.
/// A run whose record says it ended is reported and its thread freed; so is a run that left no
/// record long after it was bound. Any other run has its current question relayed.
/// </summary>
public sealed class ChatRunFollower(
    IChatRunBindingStore bindings,
    IRunOutcomeReader outcomes,
    ChatRunOutcomeNotifier notifier,
    ChatRunQuestionRelay questions,
    TimeProvider timeProvider,
    ILogger<ChatRunFollower> logger)
{
    // A claimed run has no record until its first event lands; this is how long that may take
    // before the binding is given up as a run that never started.
    internal static readonly TimeSpan GoneAfter = TimeSpan.FromMinutes(30);

    public async Task FollowOnceAsync(CancellationToken ct)
    {
        foreach (var binding in await bindings.ListOpenAsync(ct))
        {
            try { await FollowAsync(binding, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Following chat-bound run {RunId} failed this pass", binding.RunId);
            }
        }
    }

    private async Task FollowAsync(ChatRunBindingFact binding, CancellationToken ct)
    {
        var outcome = await outcomes.ReadAsync(binding.RunId, ct);
        if (outcome is { IsEnded: true } || IsGone(binding, outcome))
            await notifier.NotifyAsync(binding, outcome, ct);
        else
            await questions.RelayAsync(binding, ct);
    }

    private bool IsGone(ChatRunBindingFact binding, RunOutcome? outcome) =>
        outcome is null && timeProvider.GetUtcNow() - binding.BoundAt > GoneAfter;
}
