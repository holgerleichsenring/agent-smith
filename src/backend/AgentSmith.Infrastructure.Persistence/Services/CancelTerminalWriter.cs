using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-24-ca23: ends a run whose own terminal event nobody will deliver. A run that PAUSED
/// left the active set, so no drain reads the stream a cancel is published into — the row would
/// stay unfinished and the enforcing scan would select it again every 15 seconds, rewriting its
/// ticket each time. Both cancel entry points call this, and both get what publishing alone
/// could not give them.
/// <para>
/// It writes through <see cref="RunFinalizationProjection"/> rather than touching the row: that
/// projection owns what a terminal transition MEANS — releasing the capacity a waiting run
/// deliberately kept, and computing the cost total the enforcer's event does not carry. Its
/// set-once guard also makes the drained event a harmless no-op if it ever does arrive, so this
/// widens the single writer by a named caller instead of becoming a second implementation.
/// </para>
/// </summary>
public sealed class CancelTerminalWriter(IServiceProvider services)
{
    public async Task FinalizeAsync(RunFinishedEvent terminal, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RunFinalizationProjection>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), terminal, cancellationToken);
    }
}
