using AgentSmith.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0423: brings the local run store up to date before the first event reaches it, once
/// per process, and then gets out of the way.
/// <para>
/// The alternative was a lifecycle call in every verb — eight edits, and the ninth verb
/// silently records nothing. Recording is a property of the CLI's graph, so it belongs in
/// the graph. If the store cannot be prepared, publishing stops rather than warning once
/// per event: an unrecorded run should say so once, not flood the console the operator is
/// trying to read.
/// </para>
/// </summary>
public sealed class PreparedStoreEventPublisher(
    IServiceScopeFactory scopeFactory,
    CliRunStore store,
    IEventPublisher inner) : IEventPublisher
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool? _ready;

    public async Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        if (!await IsReadyAsync(cancellationToken)) return;
        await inner.PublishAsync(runEvent, cancellationToken);
    }

    private async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready is { } known) return known;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ready is { } raced) return raced;
            using var scope = scopeFactory.CreateScope();
            var schema = scope.ServiceProvider.GetRequiredService<CliRunRecordingSchema>();
            _ready = await schema.EnsureAsync(store.IsLocalSqlite, cancellationToken);
            return _ready.Value;
        }
        finally { _gate.Release(); }
    }
}
