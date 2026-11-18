using System.Text.Json;
using AgentSmith.Contracts.Deployment;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-18-3863f: the deploy records — read by run for the pages, and the unsettled ones loaded tracked for the
/// deploy follow, the one writer after the insert, which saves what it read through <see cref="SaveAsync"/>.
/// </summary>
public sealed class DeployRecordRepository(IUnitOfWork unitOfWork)
{
    public async Task<DeployRecordView?> ByRunAsync(string runId, CancellationToken ct) =>
        await unitOfWork.Set<DeployRecord>().AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId, ct) is { } record
            ? View(record) : null;

    /// <summary>The unsettled records, least recently checked first, at most <paramref name="cap"/>.</summary>
    public async Task<IReadOnlyList<DeployRecord>> UnsettledAsync(int cap, CancellationToken ct) =>
        await unitOfWork.Set<DeployRecord>().Where(r => r.SettledAtTicks == null)
            .OrderBy(r => r.CheckedAtTicks ?? 0).Take(cap).ToListAsync(ct);

    /// <summary>Whether any record is still followed — what the follow's cadence is needed for.</summary>
    public Task<bool> AnyUnsettledAsync(CancellationToken ct) =>
        unitOfWork.Set<DeployRecord>().AnyAsync(r => r.SettledAtTicks == null, ct);

    public Task SaveAsync(CancellationToken ct) => unitOfWork.SaveChangesAsync(ct);

    public static string? Failures(IReadOnlyList<CiFailure> failures) =>
        failures.Count == 0 ? null : JsonSerializer.Serialize(failures, JsonSerializerOptions.Web);

    public static DeployRecordView View(DeployRecord r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new DeployRecordView(r.RunId, r.Project, r.Context, r.Environment, r.Version, r.Delivery, r.Repo, r.Url,
            r.PullRequestState, r.CiState, Read(r.CiFailuresJson), At(r.DeliveredAtTicks), r.CheckedAtTicks is { } c ? At(c) : null,
            r.SettledAtTicks is { } s ? At(s) : null, r.Note);
    }

    private static DateTimeOffset At(long ticks) => new(ticks, TimeSpan.Zero);

    private static IReadOnlyList<CiFailure> Read(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<CiFailure>>(json, JsonSerializerOptions.Web) ?? [];
}
