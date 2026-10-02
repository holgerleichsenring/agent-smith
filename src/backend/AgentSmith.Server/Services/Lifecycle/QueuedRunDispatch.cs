using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5ab2b: hands a run's request to the queue with the row as its keeper. The
/// request is stored on its run row — serialized exactly as RedisJobQueue serializes it —
/// before the push, so a push Redis loses (a flush, a restart) is pushed again from the row by
/// <see cref="QueuedRunSweeper"/>. The request carries no credential, so neither does the row.
/// </summary>
public sealed class QueuedRunDispatch(
    IServiceScopeFactory scopes, IRedisJobQueue jobQueue, TimeProvider timeProvider)
{
    public async Task DispatchAsync(PipelineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunId is not { Length: > 0 } runId)
            throw new ArgumentException("A dispatched request names the run row that keeps it.", nameof(request));
        using (var scope = scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<QueuedRunRepository>().StoreRequestAsync(
                runId, JsonSerializer.Serialize(request), timeProvider.GetUtcNow(), ct);
        await jobQueue.EnqueueAsync(request, ct);
    }
}
