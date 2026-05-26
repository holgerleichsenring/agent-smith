using AgentSmith.Contracts.Dialogue;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Manages pending typed-question TCS lifecycles for Slack interactions.
/// </summary>
internal sealed class SlackTypedQuestionManager(ILogger logger)
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogAnswer?>>
        _pending = new();

    internal TaskCompletionSource<DialogAnswer?> Register(string questionId)
    {
        var tcs = new TaskCompletionSource<DialogAnswer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[questionId] = tcs;
        return tcs;
    }

    internal void Unregister(string questionId) =>
        _pending.TryRemove(questionId, out _);

    internal bool TryComplete(string questionId, DialogAnswer answer)
    {
        if (_pending.TryRemove(questionId, out var tcs))
        {
            tcs.TrySetResult(answer);
            return true;
        }

        return false;
    }

    internal bool HasPending(string questionId) =>
        _pending.ContainsKey(questionId);

    internal async Task<DialogAnswer?> WaitAsync(
        string questionId,
        DialogQuestion question,
        CancellationToken cancellationToken)
    {
        var tcs = Register(questionId);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(question.Timeout);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Typed question {QuestionId} timed out after {Timeout}",
                questionId, question.Timeout);
            return null;
        }
        finally
        {
            Unregister(questionId);
        }
    }
}
