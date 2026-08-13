using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0404: projects a sandbox's LIFETIME onto its RunSandbox row — created,
/// disposed, or vanished. Split out of <see cref="RunEventApplier"/> like
/// <see cref="RunCheckpointProjection"/>: the applier routes events, this owns
/// what a sandbox row means over its life.
/// </summary>
public sealed class RunSandboxProjection
{
    // p0332: lifetime start + declared memory request land on the row so the
    // snapshot can compute reserved resource-time (request x lifetime) per run.
    public async Task CreateAsync(IUnitOfWork uow, SandboxCreatedEvent e, CancellationToken ct)
    {
        uow.Add(new RunSandbox
        {
            RunId = e.RunId, Key = e.Repo, RepoName = e.Repo, ToolchainImage = e.Image,
            Status = "created", SpawnedAt = e.Timestamp, MemoryRequest = e.MemoryRequest,
            StepIndex = e.OriginStepIndex, // p0388a
        });
        await uow.SaveChangesAsync(ct);
    }

    public async Task DisposeAsync(IUnitOfWork uow, SandboxDisposedEvent e, CancellationToken ct)
    {
        var box = await LatestAsync(uow, e.RunId, e.Repo, ct);
        if (box is null) return;
        box.Status = e.ExitCode == 0 ? "ok" : "failed";
        // p0332: the dispose timestamp closes the sandbox lifetime window.
        box.DisposedAt ??= e.Timestamp;
        await uow.SaveChangesAsync(ct);
    }

    // p0332: a vanished sandbox (heartbeat gone + container confirmed dead) never
    // gets a SandboxDisposedEvent — the vanish verdict IS its end-of-life, so it
    // closes the lifetime window too. Was trail-only before p0332.
    public async Task MarkVanishedAsync(IUnitOfWork uow, SandboxVanishedEvent e, CancellationToken ct)
    {
        var box = await LatestAsync(uow, e.RunId, e.Repo, ct);
        if (box is null) return;
        box.Status = "vanished";
        box.DisposedAt ??= e.Timestamp;
        await uow.SaveChangesAsync(ct);
    }

    private static Task<RunSandbox?> LatestAsync(
        IUnitOfWork uow, string runId, string repo, CancellationToken ct) =>
        uow.Set<RunSandbox>()
            .Where(s => s.RunId == runId && s.RepoName == repo)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
}
