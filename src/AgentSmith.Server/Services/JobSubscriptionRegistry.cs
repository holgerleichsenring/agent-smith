using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Thread-safe registry that tracks active job subscription tasks
/// and their cancellation tokens.
/// </summary>
internal sealed class JobSubscriptionRegistry(
    ILogger logger) : IDisposable
{
    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cts)> _subscriptions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<bool> TryAddAsync(
        string jobId, Task task, CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_subscriptions.ContainsKey(jobId))
                return false;

            _subscriptions[jobId] = (task, cts);
            logger.LogInformation(
                "Started tracking job {JobId} ({Active} active jobs)",
                jobId, _subscriptions.Count);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetTrackedIdsAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _subscriptions.Keys.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CancelAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_subscriptions.TryGetValue(jobId, out var entry))
            {
                await entry.Cts.CancelAsync();
                logger.LogInformation("Cancelled subscription for orphaned job {JobId}", jobId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string jobId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_subscriptions.TryGetValue(jobId, out var entry))
            {
                entry.Cts.Dispose();
                _subscriptions.Remove(jobId);
            }

            logger.LogDebug(
                "Removed subscription for job {JobId} ({Active} remaining)",
                jobId, _subscriptions.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveCompletedAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var completed = _subscriptions
                .Where(kvp => kvp.Value.Task.IsCompleted)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var jobId in completed)
            {
                if (_subscriptions.TryGetValue(jobId, out var entry))
                {
                    entry.Cts.Dispose();
                    _subscriptions.Remove(jobId);
                }
            }

            if (completed.Count > 0)
                logger.LogDebug("Cleaned up {Count} completed job subscriptions", completed.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
