using AgentSmith.Contracts.Dialogue;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Tracks pending typed questions for Teams conversations.
/// Allows question submission (via Adaptive Card callback) to complete
/// an awaiting <see cref="TaskCompletionSource{T}"/>.
/// </summary>
public sealed class TeamsTypedQuestionTracker(
    ILogger logger)
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogAnswer?>>
        _pending = new();

    public TaskCompletionSource<DialogAnswer?> Register(string questionId)
    {
        var tcs = new TaskCompletionSource<DialogAnswer?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[questionId] = tcs;
        return tcs;
    }

    public void Unregister(string questionId)
        => _pending.TryRemove(questionId, out _);

    public bool TryComplete(string questionId, DialogAnswer answer)
    {
        if (_pending.TryRemove(questionId, out var tcs))
        {
            tcs.TrySetResult(answer);
            return true;
        }
        return false;
    }

    public bool HasPending(string questionId)
        => _pending.ContainsKey(questionId);
}
