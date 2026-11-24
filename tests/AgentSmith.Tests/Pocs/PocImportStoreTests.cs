using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: the rows an import writes and a poc-deploy run's intake records — a taken name and a passed limit
/// write nothing, revisions number per PoC, a lost claim's revision goes, the intake writes a revision once and turns a
/// never-ready PoC refused, the imported audit entry is written once per run, and a new upload replaces the earlier
/// un-imported ones.
/// </summary>
public sealed class PocImportStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocImportStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Insert_ANameALivePocHolds_IsTaken_AndWritesNothing()
    {
        await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-bob"));

        (await Imports().InsertAsync(New("todo", "ann-sub"), null, _ct)).Should().Be(PocInsertOutcome.NameTaken);
        (await _db.Set<Poc>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Insert_PastThePerPersonLimit_RollsBack()
    {
        await Imports().InsertAsync(New("one", "ann-sub"), 1, _ct);

        (await Imports().InsertAsync(New("two", "ann-sub"), 1, _ct)).Should().Be(PocInsertOutcome.OverLimit);
        (await _db.Set<Poc>().AsNoTracking().Select(p => p.Name).ToListAsync()).Should().Equal("one");
        (await Imports().LiveCountAsync("ann-sub", "main", _ct)).Should().Be(1);
    }

    [Fact]
    public async Task Revisions_NumberPerPoc_AndALostClaimsRevisionIsDeleted()
    {
        var poc = await PocRows.AddAsync(_db, PocRows.Poc("todo", "poc-ann"));
        var first = await Imports().AddRevisionAsync(poc.Id, "s", "u1", "ann-sub", _ct);
        var second = await Imports().AddRevisionAsync(poc.Id, "s", "u2", "ann-sub", _ct);

        await Imports().DeleteUnclaimedRevisionAsync(second.Id, _ct);

        (first.Number, second.Number).Should().Be((1, 2));
        (await Imports().RevisionOfUploadAsync("u2", _ct)).Should().BeNull();
        (await Imports().ImportedUploadIdsAsync("s", _ct)).Should().Equal("u1");
        (await Imports().OwnsInProjectAsync("poc-ann-sub", "poc-ann", _ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Revive_ARefusedPoc_GoesBackToImporting()
    {
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.State = PocStates.Refused;
        await PocRows.AddAsync(_db, poc);

        await Imports().ReviveAsync(poc.Id, _ct);

        (await _db.Set<Poc>().AsNoTracking().SingleAsync()).State.Should().Be(PocStates.Importing);
    }

    [Fact]
    public async Task Intake_ARefusal_RefusesANeverReadyPoc_AndIsWrittenOnce()
    {
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.State = PocStates.Importing;
        await PocRows.AddAsync(_db, poc);
        await Imports().AddRevisionAsync(poc.Id, "s", "u1", "ann-sub", _ct);
        var intake = new PocIntakeRepository(_db, new ReferenceSetRepository(_db));

        await intake.RecordAsync(new PocIntakeOutcome(poc.Id, 1, "run-1", PocRevisionStates.Refused, null, null, null, null, "'.env' is forbidden"), _ct);
        await intake.RecordAsync(new PocIntakeOutcome(poc.Id, 1, "run-2", PocRevisionStates.Validated, "{}", 8000, null, null, null), _ct);

        var subject = await intake.ReadAsync(poc.Id, 1, _ct);
        subject!.RevisionState.Should().Be(PocRevisionStates.Refused);
        subject.PocState.Should().Be(PocStates.Refused);
        (await _db.Set<PocRevision>().AsNoTracking().SingleAsync()).Reason.Should().Be("'.env' is forbidden");
        (await intake.ReadAsync(poc.Id, 2, _ct)).Should().BeNull();
    }

    [Fact]
    public async Task Intake_ReadsTheStoredArchive_OnlyUnderItsSession()
    {
        var files = new ReferenceSetRepository(_db);
        var set = await files.AddAsync("poc-upload:a", [new ReferenceFile { RelativePath = "app.zip", MediaType = "application/zip", Content = [1, 2] }], _ct);
        var intake = new PocIntakeRepository(_db, files);

        (await intake.ArchiveAsync("poc-upload:a", set.SetId, _ct)).Should().Equal(1, 2);
        (await intake.ArchiveAsync("poc-upload:b", set.SetId, _ct)).Should().BeNull();
    }

    [Fact]
    public async Task Uploads_DeleteSetsExcept_KeepsTheNamedSetsAndOtherSessions()
    {
        var files = new ReferenceSetRepository(_db);
        var kept = await files.AddAsync("s", [File()], _ct);
        await files.AddAsync("s", [File()], _ct);
        var other = await files.AddAsync("t", [File()], _ct);

        (await files.DeleteSetsExceptAsync("s", [kept.SetId], _ct)).Should().Be(1);
        (await files.ListAsync("s", _ct)).Select(x => x.SetId).Should().Equal(kept.SetId);
        (await files.ListAsync("t", _ct)).Select(x => x.SetId).Should().Equal(other.SetId);
    }

    [Fact]
    public async Task ImportedProjection_WritesTheAuditEntryOncePerRun()
    {
        var projection = new PocImportedProjection();
        var imported = new PocImportedEvent("run-1", DateTimeOffset.UtcNow, 7, 1, "ann-sub");

        await projection.ApplyAsync(_db, imported, null, _ct);
        await projection.ApplyAsync(_db, imported, null, _ct);

        var entry = (await new PocAuditRepository(_db).ListAsync(7, _ct)).Should().ContainSingle().Subject;
        (entry.Act, entry.Actor, entry.RunId).Should().Be((PocActs.Imported, "ann-sub", "run-1"));
    }

    private PocImportRepository Imports() => new(_db, new SqliteUniqueViolationTranslator());

    private static ReferenceFile File() => new() { RelativePath = "app.zip", MediaType = "application/zip", Content = [1] };

    private static Poc New(string name, string owner) => new()
    {
        Name = name, LiveName = name, Project = "poc-ann", Target = "main", Folder = $"pocs/poc-ann/{name}", Owner = owner,
        OwnerName = owner, State = PocStates.Importing,
    };
}
