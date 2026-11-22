using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-20-24bd: the model places in the database, for the singleton gate — a scope per operation, the
/// <see cref="DbCapacityBudget"/> pattern. A place is taken by INSERT and the unique (Key, Slot) index refuses a
/// taken one, so the bound holds across replicas and providers without a read-sum-write. A release tells this
/// process's waiters at once; a waiter on another replica finds the place on its next poll. The waits are kept
/// beside the places (<see cref="ModelRunWait"/>), read by both wall-time clocks.
/// </summary>
public sealed class DbModelRunSlots(
    IServiceScopeFactory scopeFactory, ModelRunSlotSignal signal, TimeProvider clock) : IModelRunSlots
{
    public async Task<bool> AcquireAsync(string key, int limit, string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (await unitOfWork.Set<ModelRunSlot>().AsNoTracking().AnyAsync(s => s.RunId == runId, cancellationToken))
            return await ClosedWaitAsync(unitOfWork, runId, cancellationToken); // one place per run, on whichever key first
        var taken = await unitOfWork.Set<ModelRunSlot>().AsNoTracking()
            .Where(s => s.Key == key).Select(s => s.Slot).ToListAsync(cancellationToken);
        if (taken.Count >= limit) return false; // a lowered limit admits nobody until enough have left
        var violations = scope.ServiceProvider.GetRequiredService<IUniqueViolationTranslator>();
        var now = clock.GetUtcNow();
        var wait = await OpenWaitAsync(unitOfWork, runId, cancellationToken);
        foreach (var slot in Enumerable.Range(0, limit).Where(i => !taken.Contains(i)))
        {
            var entry = unitOfWork.Add(new ModelRunSlot { Key = key, Slot = slot, RunId = runId, AcquiredAt = now });
            wait?.Close(now); // the place and the end of the wait are saved together
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
            {
                entry.State = EntityState.Detached; // another run took this place first; try the next (the wait stays closed)
            }
        }
        return false;
    }

    public async Task ReleaseAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.Set<ModelRunSlot>().Where(s => s.RunId == runId).ExecuteDeleteAsync(cancellationToken);
            await unitOfWork.Set<ModelRunWait>().Where(w => w.RunId == runId).ExecuteDeleteAsync(cancellationToken);
        }
        finally
        {
            signal.Released(runId); // 2026-11-22-9719a: the process forgets the run even when the delete failed; the sweep frees the row
        }
    }

    public Task WaitStartedAsync(string runId, CancellationToken cancellationToken) =>
        UpdateWaitAsync(runId, (wait, now) => wait.WaitingSince ??= now, cancellationToken);

    public Task WaitEndedAsync(string runId, CancellationToken cancellationToken) =>
        UpdateWaitAsync(runId, (wait, now) => wait.Close(now), cancellationToken);

    public async Task<IReadOnlyDictionary<string, TimeSpan>> WaitedAsync(
        IReadOnlyCollection<string> runIds, CancellationToken cancellationToken)
    {
        if (runIds.Count == 0) return new Dictionary<string, TimeSpan>();
        using var scope = scopeFactory.CreateScope();
        var waits = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<ModelRunWait>().AsNoTracking()
            .Where(w => runIds.Contains(w.RunId)).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return waits.ToDictionary(w => w.RunId, w => w.Waited(now), StringComparer.Ordinal);
    }

    public Task<int> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) =>
        new ModelRunSlotSweep(scopeFactory, signal, clock).RunAsync(staleAfter, cancellationToken);

    private static async Task<ModelRunWait?> OpenWaitAsync(IUnitOfWork unitOfWork, string runId, CancellationToken ct) =>
        await unitOfWork.Set<ModelRunWait>().FirstOrDefaultAsync(w => w.RunId == runId && w.WaitingSince != null, ct);

    private async Task<bool> ClosedWaitAsync(IUnitOfWork unitOfWork, string runId, CancellationToken ct)
    {
        if (await OpenWaitAsync(unitOfWork, runId, ct) is not { } wait) return true;
        wait.Close(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }


    private async Task UpdateWaitAsync(string runId, Action<ModelRunWait, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var wait = await unitOfWork.Set<ModelRunWait>().FirstOrDefaultAsync(w => w.RunId == runId, cancellationToken);
        if (wait is null) unitOfWork.Add(wait = new ModelRunWait { RunId = runId });
        change(wait, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken); // a run waits on one flow at a time (the gate's per-run entry)
    }
}
