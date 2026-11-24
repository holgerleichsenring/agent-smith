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

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>2026-11-23-c81ed, migrated store: what an act reads, what its delete commit and settled follow write, the
/// audit projection once per run and act, and a freed name taken again.</summary>
public sealed class PocActStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocActStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task Read_ReturnsTheRowsStateAndDesiredState_AndARevisionsDigestAndRolloutState()
    {
        var poc = await SeedAsync();
        var store = new PocActRepository(_db);

        (await store.ReadAsync(poc.Id, _ct)).Should().Be(new PocActSubject(poc.Id, "shop", "main", PocStates.Ready, 1, 1, PocRows.Digest, null)
        {
            Lifetime = new PocLifetimeFacts(poc.CreatedAt, null, null, 0), // 2026-11-23-c81ee
        });
        (await store.RevisionAsync(poc.Id, 1, _ct)).Should().Be(new PocActRevision(1, PocRows.Digest, "abc", PocRolloutStates.Healthy));
        (await store.RevisionAsync(poc.Id, 9, _ct)).Should().BeNull();
    }

    [Fact]
    public async Task RecordAbsent_ThenRecordDeleted_NullTheDesiredState_ThenFreeTheName_SoANewPocTakesIt()
    {
        var poc = await SeedAsync();
        await new PocGitOpsRepository(_db).RecordAbsentAsync(poc.Id, _ct);
        var pending = await _db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == poc.Id);
        (pending.Revision, pending.ImageDigest, pending.State, pending.LiveName).Should().Be(((int?)null, (string?)null, PocStates.Ready, "shop"));

        await new PocRolloutRepository(_db).RecordDeletedAsync(poc.Id, _ct);
        await PocRows.AddAsync(_db, PocRows.Poc("shop", "poc-bob"));

        var rows = await _db.Set<Poc>().AsNoTracking().Where(p => p.Name == "shop").OrderBy(p => p.Id).ToListAsync();
        rows.Select(p => (p.State, p.LiveName)).Should().Equal((PocStates.Deleted, null), (PocStates.Ready, "shop"));
    }

    [Fact]
    public async Task RecordOutcome_WithoutARolloutState_LeavesTheRevisionsUntouched()
    {
        var poc = await SeedAsync();

        await new PocRolloutRepository(_db).RecordOutcomeAsync(new PocRolloutOutcome(poc.Id, 1, null, DateTimeOffset.UtcNow, PocStates.Stopped), _ct);

        (await _db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == poc.Id)).State.Should().Be(PocStates.Stopped);
        (await _db.Set<PocRevision>().AsNoTracking().SingleAsync()).Should().Match<PocRevision>(r =>
            r.RolloutState == PocRolloutStates.Healthy && r.RolloutAtTicks == 5);
    }

    [Fact]
    public async Task ActedProjection_WritesOneEntryPerRunAndAct_AndAReplayInsertsNone()
    {
        var poc = await SeedAsync();
        var stop = new PocActedEvent("run-s", DateTimeOffset.UtcNow, poc.Id, PocActKinds.Stop, "ann-sub", null, 1, "sha2");
        var apply = new PocActedEvent("run-a", DateTimeOffset.UtcNow, poc.Id, PocActKinds.Apply, "ann-sub", "applied", 1, null);
        var delete = new PocActedEvent("run-d", DateTimeOffset.UtcNow, poc.Id, PocActKinds.Delete, "ann-sub", null, null, "sha3");

        foreach (var e in new[] { stop, stop, apply, delete, delete with { CommitSha = null } })
            await new PocActedProjection().ApplyAsync(_db, e, null, _ct);

        var entries = await _db.Set<PocAuditEntry>().AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        entries.Select(a => (a.Act, a.RunId)).Should().Equal(
            (PocActs.Stopped, "run-s"), (PocActs.Deployed, "run-a"), (PocActs.Deleted, "run-d"));
        entries[1].Reason.Should().StartWith("applied");
        entries[2].Reason.Should().Be("commit sha3");
    }

    private async Task<Poc> SeedAsync()
    {
        var poc = PocRows.Poc("shop", "poc-ann");
        await PocRows.AddAsync(_db, poc);
        _db.Add(new PocRevision
        {
            PocId = poc.Id, Number = 1, UploadSession = "s", UploadId = "u1", State = PocRevisionStates.Built, Actor = "ann-sub",
            CommitSha = "abc", Digest = PocRows.Digest, RolloutState = PocRolloutStates.Healthy, RolloutAtTicks = 5,
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
