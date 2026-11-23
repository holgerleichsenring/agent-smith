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

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933dc, migrated store: a blocked verdict blocks a never-ready PoC and leaves a ready one serving; a passing
/// one sets a blocked PoC back to importing; overruled ids append under their stage; a refused intake of a blocked PoC
/// refuses it; an overrule event becomes one 'overruled' audit entry with who, why and the ids.
/// </summary>
public sealed class PocScanStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly CancellationToken _ct = CancellationToken.None;

    public PocScanStoreTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Theory]
    [InlineData(PocStates.Importing, PocStates.Blocked)]
    [InlineData(PocStates.Blocked, PocStates.Blocked)]
    [InlineData(PocStates.Ready, PocStates.Ready)]
    public async Task RecordVerdict_Blocked_BlocksOnlyANeverReadyPoc(string before, string after)
    {
        var poc = await SeedAsync(before);

        await Scans().RecordVerdictAsync(new PocScanOutcome(poc.Id, 1, PocRevisionStates.Blocked, "{}", "why"), _ct);

        (await StateAsync(poc.Id)).Should().Be(after);
        var subject = (await Scans().ReadAsync(poc.Id, 1, _ct))!;
        (subject.RevisionState, subject.VerdictJson, subject.LatestRevision).Should().Be((PocRevisionStates.Blocked, "{}", 1));
    }

    [Fact]
    public async Task RecordVerdict_APassAfterABlock_SetsThePocBackToImporting_AndKeepsTheExplanation()
    {
        var poc = await SeedAsync(PocStates.Blocked);
        await Scans().RecordVerdictAsync(new PocScanOutcome(poc.Id, 1, PocRevisionStates.Blocked, "{}", "why"), _ct);

        await Scans().RecordVerdictAsync(new PocScanOutcome(poc.Id, 1, PocRevisionStates.Overruled, "{}", null), _ct);

        (await StateAsync(poc.Id)).Should().Be(PocStates.Importing);
        (await _db.Set<PocRevision>().AsNoTracking().SingleAsync()).Explanation.Should().Be("why");
    }

    [Fact]
    public async Task AppendOverruled_KeepsAnotherStagesIds_AndEarlierOverrules()
    {
        var poc = await SeedAsync(PocStates.Blocked);
        await Scans().AppendOverruledAsync(poc.Id, 1, PocScanStages.Image, ["trivy:a"], _ct);
        await Scans().AppendOverruledAsync(poc.Id, 1, PocScanStages.Code, ["osv:1"], _ct);
        await Scans().AppendOverruledAsync(poc.Id, 1, PocScanStages.Code, ["osv:2"], _ct);

        var ids = PocOverruledIds.FromJson((await Scans().ReadAsync(poc.Id, 1, _ct))!.OverruledIdsJson);

        ids.Of(PocScanStages.Image).Should().Equal("trivy:a");
        ids.Of(PocScanStages.Code).Should().Equal("osv:1", "osv:2");
    }

    [Fact]
    public async Task RecordCommit_StoresTheSha()
    {
        var poc = await SeedAsync(PocStates.Importing);

        await Scans().RecordCommitAsync(poc.Id, 1, "abc", _ct);

        (await Scans().ReadAsync(poc.Id, 1, _ct))!.CommitSha.Should().Be("abc");
    }

    [Fact]
    public async Task Intake_ARefusalOfABlockedPoc_RefusesIt()
    {
        var poc = await SeedAsync(PocStates.Blocked, PocRevisionStates.Admitted);

        await new PocIntakeRepository(_db, new ReferenceSetRepository(_db)).RecordAsync(
            new PocIntakeOutcome(poc.Id, 1, "run-1", PocRevisionStates.Refused, null, null, null, null, "bad"), _ct);

        (await StateAsync(poc.Id)).Should().Be(PocStates.Refused);
    }

    [Fact]
    public async Task OverruledProjection_WritesOneEntryWithWhoWhyAndTheIds()
    {
        var poc = await SeedAsync(PocStates.Blocked);
        var e = new PocOverruledEvent("run-9", DateTimeOffset.UtcNow, poc.Id, 1, PocScanStages.Code, "admin-sub", "demo only", ["osv:1", "osv:2"]);

        await new PocOverruledProjection().ApplyAsync(_db, e, null, _ct);
        await new PocOverruledProjection().ApplyAsync(_db, e, null, _ct);

        var entry = await _db.Set<PocAuditEntry>().AsNoTracking().SingleAsync();
        (entry.Act, entry.Actor, entry.Reason, entry.RunId).Should().Be((PocActs.Overruled, "admin-sub", "demo only", "run-9"));
        entry.FindingIdsJson.Should().Be("""["osv:1","osv:2"]""");
    }

    private PocScanRepository Scans() => new(_db);

    private async Task<Poc> SeedAsync(string state, string revisionState = PocRevisionStates.Validated)
    {
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.State = state;
        await PocRows.AddAsync(_db, poc);
        _db.Add(new PocRevision { PocId = poc.Id, Number = 1, UploadSession = "s", UploadId = "u1", State = revisionState, Actor = "ann-sub" });
        await _db.SaveChangesAsync(_ct);
        return poc;
    }

    private async Task<string> StateAsync(long pocId) => (await _db.Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == pocId)).State;

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
