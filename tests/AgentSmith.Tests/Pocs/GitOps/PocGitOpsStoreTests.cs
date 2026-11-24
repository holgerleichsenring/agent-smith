using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb, migrated store: the desired state and fingerprint are written together, a revision reads
/// its port, commit and digest, and a deployed event becomes one 'deployed' audit entry however often it arrives.</summary>
public sealed class PocGitOpsStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocGitOpsStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task RecordDeployed_WritesTheDesiredStateAndFingerprint_AndClearDropsTheFingerprint()
    {
        var poc = await SeedAsync();
        var store = new PocGitOpsRepository(_db);

        await store.RecordDeployedAsync(poc.Id, new PocDesiredState(0, 1, PocRenderRig.Digest, "/health"), "hmac-sha256:" + new string('b', 64), _ct);
        var read = await store.ReadAsync(poc.Id, _ct);
        await store.ClearSealFingerprintAsync(poc.Id, _ct);

        (read!.Replicas, read.Revision, read.ImageDigest, read.ReadinessPath).Should().Be((0, 1, PocRenderRig.Digest, "/health"));
        read.SealFingerprint.Should().StartWith("hmac-sha256:");
        (await store.ReadAsync(poc.Id, _ct))!.SealFingerprint.Should().BeNull();
        (await store.RevisionAsync(poc.Id, 1, _ct)).Should().Be(new PocRenderRevision(1, 8080, "abc", PocRenderRig.Digest));
    }

    [Fact]
    public async Task DeployedProjection_WritesOneEntry_AReplayOrASecondEventOfTheRunInsertsNone()
    {
        var poc = await SeedAsync();
        var first = new PocDeployedEvent("run-1", DateTimeOffset.UtcNow, poc.Id, 1, PocRenderRig.Digest, "sha1", "ann-sub");

        await new PocDeployedProjection().ApplyAsync(_db, first, null, _ct);
        await new PocDeployedProjection().ApplyAsync(_db, first, null, _ct);
        await new PocDeployedProjection().ApplyAsync(_db, first with { CommitSha = null }, null, _ct);

        var entry = await _db.Set<PocAuditEntry>().AsNoTracking().SingleAsync();
        (entry.Act, entry.Actor, entry.RunId).Should().Be((PocActs.Deployed, "ann-sub", "run-1"));
        entry.Reason.Should().Contain("revision 1").And.Contain("sha1");
    }

    private async Task<Poc> SeedAsync()
    {
        var poc = PocRows.Poc("shop", "poc-ann");
        await PocRows.AddAsync(_db, poc);
        _db.Add(new PocRevision
        {
            PocId = poc.Id, Number = 1, UploadSession = "s", UploadId = "u1", State = PocRevisionStates.Built, Actor = "ann-sub",
            Port = 8080, CommitSha = "abc", Digest = PocRenderRig.Digest,
        });
        await _db.SaveChangesAsync(_ct);
        return poc;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
