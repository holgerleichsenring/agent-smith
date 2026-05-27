using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Per-pipeline-run concurrency cap. Holds a SemaphoreSlim sized from
/// <see cref="LoopLimitsConfig.MaxConcurrentSkillCalls"/>. Registered as scoped
/// so each DI scope (one per pipeline run) gets its own semaphore — cross-run
/// rate-limit contention is an external concern.
/// </summary>
public sealed class PipelineConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public PipelineConcurrencyGate(LoopLimitsConfig limits)
    {
        var size = Math.Max(1, limits.MaxConcurrentSkillCalls);
        _semaphore = new SemaphoreSlim(size, size);
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new ReleaseHandle(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class ReleaseHandle(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}
