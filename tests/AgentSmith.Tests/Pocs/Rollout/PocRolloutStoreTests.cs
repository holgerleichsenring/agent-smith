using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ec, migrated store: the outcome writes the revision's rollout state and the PoC state together,
/// a null state keeps the PoC's, and the Rollout page finds the revision a run took in.</summary>
public sealed class PocRolloutStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocRolloutStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task RecordOutcome_WritesTheRevisionAndThePocState_ANullStateKeepsIt()
    {
        var poc = await SeedAsync();
        var store = new PocRolloutRepository(_db);
        var at = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        (await store.HasHealthyRolloutAsync(poc.Id, _ct)).Should().BeFalse();
        await store.RecordOutcomeAsync(new PocRolloutOutcome(poc.Id, 1, PocRolloutStates.Healthy, at, PocStates.Ready), _ct);
        await store.RecordOutcomeAsync(new PocRolloutOutcome(poc.Id, 2, PocRolloutStates.Failed, at, null), _ct);
        await store.RecordCommitAsync(poc.Id, 2, "c0ffee", _ct);

        (await _db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == poc.Id)).State.Should().Be(PocStates.Ready);
        var rows = await _db.Set<PocRevision>().AsNoTracking().Where(r => r.PocId == poc.Id).OrderBy(r => r.Number).ToListAsync();
        rows.Select(r => (r.RolloutState, r.RolloutAtTicks)).Should().Equal((PocRolloutStates.Healthy, at.UtcTicks), (PocRolloutStates.Failed, at.UtcTicks));
        rows[1].RolloutCommitSha.Should().Be("c0ffee");
        (await store.HasHealthyRolloutAsync(poc.Id, _ct)).Should().BeTrue();
    }

    [Fact]
    public async Task RevisionOfRun_IsTheRunsRevision_ElseTheCurrentOne()
    {
        var poc = await SeedAsync();
        poc.Revision = 1;
        var store = new PocRolloutRepository(_db);

        (await store.RevisionOfRunAsync(poc, "run-2", _ct))!.Number.Should().Be(2);
        (await store.RevisionOfRunAsync(poc, "run-act", _ct))!.Number.Should().Be(1);
        (await store.RunStartedAtAsync("run-none", _ct)).Should().BeNull();
    }

    private async Task<Poc> SeedAsync()
    {
        var poc = PocRows.Poc("shop", "poc-ann");
        await PocRows.AddAsync(_db, poc);
        foreach (var n in new[] { 1, 2 })
            _db.Add(new PocRevision
            {
                PocId = poc.Id, Number = n, UploadSession = "s", UploadId = $"u{n}", State = PocRevisionStates.Built, Actor = "ann-sub",
                RunId = $"run-{n}",
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
