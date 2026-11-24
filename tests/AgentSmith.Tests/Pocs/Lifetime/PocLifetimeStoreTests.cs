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

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee, on SQLite: the live PoCs with their lifetime facts, latest 'expired' entry and whether a blocked
/// never-deployed PoC's upload is gone — no DateTimeOffset compared in a query; the two writes; and the act projection
/// recording an expiry's or an extension's audit act with its reason.</summary>
public sealed class PocLifetimeStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;

    public PocLifetimeStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    private async Task<Poc> BlockedAsync(string name, bool keepUpload)
    {
        var poc = PocRows.Poc(name, "poc-ann");
        (poc.State, poc.Revision, poc.ImageDigest) = (PocStates.Blocked, null, null);
        await PocRows.AddAsync(_db, poc);
        _db.Add(new PocRevision { PocId = poc.Id, Number = 1, UploadSession = "poc-upload:aa", UploadId = $"{name}-1", State = PocRevisionStates.Blocked, Actor = "ann" });
        if (keepUpload)
            _db.Add(new ReferenceFile { SessionId = "poc-upload:aa", SetId = $"{name}-1", Kind = "file", RelativePath = "a.zip", MediaType = "application/zip", Content = [1] });
        await _db.SaveChangesAsync();
        return poc;
    }

    [Fact]
    public async Task Live_ReadsFactsTheLatestExpiredEntry_AndWhetherABlockedPocsUploadIsGone()
    {
        var ready = await PocRows.AddAsync(_db, PocRows.Poc("ready", "poc-ann"));
        var kept = await BlockedAsync("kept", keepUpload: true);
        var gone = await BlockedAsync("gone", keepUpload: false);
        await PocRows.AddAsync(_db, PocRows.Poc("dead", "poc-ann", deleted: true));
        _db.AddRange(PocRows.Entry(ready.Id, PocActs.Expired, "r1", at: 100), PocRows.Entry(ready.Id, PocActs.Expired, "r2", at: 300),
            PocRows.Entry(ready.Id, PocActs.Stopped, "r3", at: 900));
        await _db.SaveChangesAsync();

        var live = await new PocLifetimeRepository(_db).LiveAsync(default);

        live.Select(s => s.Name).Should().Equal("ready", "kept", "gone");
        live[0].Should().Match<PocLifetimeSubject>(s => s.LatestExpiredAtTicks == 300 && s.Lifetime.CreatedAt == ready.CreatedAt && !s.UploadGone);
        (live[1].UploadGone, live[2].UploadGone).Should().Be((false, true));
        (await new PocLifetimeRepository(_db).ReadAsync(kept.Id, default))!.Revision.Should().BeNull();
        gone.Id.Should().Be(live[2].PocId);
    }

    [Fact]
    public async Task Extend_CountsAndStoresTheExpiry_Seen_StoresTheTime()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("ext", "poc-ann"));
        var repository = new PocLifetimeRepository(_db);

        await repository.ExtendAsync(poc.Id, 500, default);
        await repository.ExtendAsync(poc.Id, 900, default);
        await repository.RecordReminderSeenAsync(poc.Id, 42, default);

        var row = await _db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == poc.Id);
        (row.Extensions, row.ExpiresAtTicks, row.ReminderSeenAtTicks).Should().Be((2, 900L, 42L));
    }

    [Fact]
    public async Task Projection_RecordsTheAuditActInPlaceOfTheActsOwn_WithItsReason()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("audit", "poc-ann"));
        var projection = new PocActedProjection();
        var at = DateTimeOffset.UtcNow;

        await projection.ApplyAsync(_db, new PocActedEvent("run-stop", at, poc.Id, PocActKinds.Stop, "poc-lifetime",
            "stopped, not deleted: no reminder was seen before expiry", 2, null, PocActs.Expired), null, default);
        await projection.ApplyAsync(_db, new PocActedEvent("run-ext", at, poc.Id, PocActs.Extended, "ann", "extension 1 of 2", null, null,
            PocActs.Extended), null, default);

        var entries = await new PocAuditRepository(_db).ListAsync(poc.Id, default);
        entries.Select(e => (e.Act, e.Reason)).Should().BeEquivalentTo(new[]
        {
            (PocActs.Expired, "stopped, not deleted: no reminder was seen before expiry, revision 2"), (PocActs.Extended, "extension 1 of 2"),
        });
        (await new PocAuditRepository(_db).LatestAsync([poc.Id], PocActs.Expired, default))[poc.Id].Should().Be(at.UtcTicks);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
