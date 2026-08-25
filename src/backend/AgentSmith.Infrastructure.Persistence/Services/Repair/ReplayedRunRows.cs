using System.Globalization;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-08-25-61f1: reads the repair's candidate rows out of the four tables a replay
/// duplicated, and states what makes two of them the same recorded fact.
/// <para>
/// The trail says it exactly: one row per position. The other three have no identity on
/// rows written before this phase — the column that carries one is what this phase adds —
/// so for those the fact IS the content, which is sound precisely because the caller has
/// already established that this run was replayed. Only columns that predate this phase are
/// read, so the repair runs on the schema the store is already on, ahead of the migration.
/// </para>
/// </summary>
public sealed class ReplayedRunRows
{
    public async Task<IReadOnlyList<RepairRow>> TrailAsync(
        IUnitOfWork uow, IReadOnlyList<string> runs, CancellationToken ct) =>
        [.. (await uow.Set<Entities.RunEvent>().AsNoTracking().Where(e => runs.Contains(e.RunId))
                .Select(e => new { e.Id, e.RunId, e.Seq, e.CreatedAt }).ToListAsync(ct))
            .Select(e => new RepairRow(e.Id, Key(e.RunId, e.Seq), e.CreatedAt))];

    public async Task<IReadOnlyList<RepairRow>> StepsAsync(
        IUnitOfWork uow, IReadOnlyList<string> runs, CancellationToken ct) =>
        [.. (await uow.Set<RunStep>().AsNoTracking().Where(s => runs.Contains(s.RunId))
                .Select(s => new { s.Id, s.RunId, s.StepIndex, s.StepName, s.Status, s.CreatedAt }).ToListAsync(ct))
            .Select(s => new RepairRow(s.Id, Key(s.RunId, s.StepIndex, s.StepName, s.Status), s.CreatedAt))];

    public async Task<IReadOnlyList<RepairRow>> LlmCallsAsync(
        IUnitOfWork uow, IReadOnlyList<string> runs, CancellationToken ct) =>
        [.. (await uow.Set<RunLlmCall>().AsNoTracking().Where(c => runs.Contains(c.RunId))
                .Select(c => new
                {
                    c.Id, c.RunId, c.Role, c.Phase, c.Model,
                    c.TokensIn, c.TokensOut, c.CostUsd, c.DurationMs, c.CreatedAt,
                }).ToListAsync(ct))
            .Select(c => new RepairRow(
                c.Id,
                Key(c.RunId, c.Role, c.Phase, c.Model, c.TokensIn, c.TokensOut, c.CostUsd, c.DurationMs),
                c.CreatedAt))];

    public async Task<IReadOnlyList<RepairRow>> DecisionsAsync(
        IUnitOfWork uow, IReadOnlyList<string> runs, CancellationToken ct) =>
        [.. (await uow.Set<RunDecision>().AsNoTracking().Where(d => runs.Contains(d.RunId))
                .Select(d => new { d.Id, d.RunId, d.Name, d.Reason, d.CreatedAt }).ToListAsync(ct))
            .Select(d => new RepairRow(d.Id, Key(d.RunId, d.Name, d.Reason), d.CreatedAt))];

    // A unit separator, so a value that contains the separator cannot forge a different row's key.
    private static string Key(params object?[] parts) =>
        string.Join('\u001f', parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)));
}
