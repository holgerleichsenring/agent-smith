using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-10-13-9a59i: every sandbox a run spawned, in spawn order, each with its own times and how it ended —
/// unlike the snapshot's live compute, which keeps one pod per repository. Read once per attempt, so it is
/// its own endpoint rather than a field every listed run pays for. The sandboxes no row records are named.
/// </summary>
public sealed class RunComputeReader(IServiceScopeFactory scopes)
{
    /// <summary>The sandboxes a run opens that leave no row: named, never counted.</summary>
    public static IReadOnlyList<RunComputeUncountedView> Uncounted { get; } =
    [
        new("source-scope", "reference and template sandboxes (run_in_reference, templates)"),
        new("browser", "browser sandboxes"),
        new("preflight", "the preflight round-trip probe"),
    ];

    /// <summary>Null when the run does not exist.</summary>
    public async Task<RunComputeRecordView?> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (!await uow.Set<Run>().AsNoTracking().AnyAsync(r => r.Id == runId, ct)) return null;
        var spawns = await uow.Set<RunSandbox>().AsNoTracking()
            .Where(s => s.RunId == runId).OrderBy(s => s.Id)
            .Select(s => new RunComputeSpawnView(
                s.RepoName ?? s.Key, s.SpawnedAt, s.DisposedAt, s.MemoryRequest, s.EndReason, s.ExitCode, s.OomKilled))
            .ToListAsync(ct);
        return new RunComputeRecordView(runId, spawns, Uncounted);
    }
}

/// <summary>2026-10-13-9a59i: a run's compute record — each spawn, and the sandboxes it does not count.</summary>
public sealed record RunComputeRecordView(
    string RunId, IReadOnlyList<RunComputeSpawnView> Spawns, IReadOnlyList<RunComputeUncountedView> Uncounted);

/// <summary>
/// 2026-10-13-9a59i: one spawned sandbox. EndReason is one of SandboxEndReasons; an 'unobserved' spawn keeps
/// DisposedAt null — the run detail charges it to run end.
/// </summary>
public sealed record RunComputeSpawnView(
    string Repo, DateTimeOffset? SpawnedAt, DateTimeOffset? DisposedAt, string? Memory,
    string? EndReason, int? ExitCode, bool? OomKilled);

/// <summary>2026-10-13-9a59i: a kind of sandbox the run may open that is recorded nowhere.</summary>
public sealed record RunComputeUncountedView(string Kind, string Label);
