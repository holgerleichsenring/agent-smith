using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: the PoC run gate — a claim moves Poc.LastRunId and writes the queued run row in one transaction, or
/// neither; a live run refuses a second; a finished, missing or orphaned queued run is free; a run this request just
/// awaited may be handed over.
/// </summary>
public sealed class PocRunGateTests : IDisposable
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"poc-gate-{Guid.NewGuid():N}.db");
    private readonly CancellationToken _ct = CancellationToken.None;

    // On disk, so two claims run on two connections the way two replicas would.
    public PocRunGateTests() => MigratedStoreTemplate.CopyToFile(_path);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task Claim_AFreePoc_MovesLastRunIdAndWritesTheQueuedRow_StampingTheRevision()
    {
        var (poc, revision) = await SeedAsync();

        var claim = await ClaimAsync(poc.Id, "run-1", revision.Id);

        claim.Won.Should().BeTrue();
        await using var db = Db();
        (await db.Set<Poc>().SingleAsync(p => p.Id == poc.Id)).LastRunId.Should().Be("run-1");
        (await db.Runs.SingleAsync(r => r.Id == "run-1")).Status.Should().Be("queued");
        (await db.Set<PocRevision>().SingleAsync()).RunId.Should().Be("run-1");
    }

    [Fact]
    public async Task Claim_ALiveRun_IsRefusedNamingIt_AndWritesNoRow()
    {
        var (poc, _) = await SeedAsync();
        await ClaimAsync(poc.Id, "run-1");

        var second = await ClaimAsync(poc.Id, "run-2");

        second.Should().Be(PocClaim.HeldBy("run-1"));
        await using var db = Db();
        (await db.Runs.AnyAsync(r => r.Id == "run-2")).Should().BeFalse();
    }

    [Fact]
    public async Task Claim_AFinishedOrMissingRun_IsReplaced()
    {
        var (poc, _) = await SeedAsync(lastRunId: "gone");
        (await ClaimAsync(poc.Id, "run-1")).Won.Should().BeTrue("a missing run row is free");
        await using (var db = Db())
        {
            (await db.Runs.SingleAsync(r => r.Id == "run-1")).FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        (await ClaimAsync(poc.Id, "run-2")).Won.Should().BeTrue("a finished run is free");
    }

    [Fact]
    public async Task Claim_AnOrphanedQueuedRowOlderThanStaleAfter_IsFree()
    {
        var (poc, _) = await SeedAsync();
        await ClaimAsync(poc.Id, "run-1");
        await using (var db = Db())
        {
            (await db.Runs.SingleAsync(r => r.Id == "run-1")).StartedAt = DateTimeOffset.UtcNow - StaleAfter - TimeSpan.FromMinutes(1);
            await db.SaveChangesAsync();
        }

        (await ClaimAsync(poc.Id, "run-2")).Won.Should().BeTrue("nothing ever ends a queued row no process started");
    }

    [Fact]
    public async Task Claim_TheRunThisRequestAwaited_IsHandedOver_WhileItsRowIsStillOpen()
    {
        var (poc, _) = await SeedAsync();
        await ClaimAsync(poc.Id, "variables-run");

        (await ClaimAsync(poc.Id, "deploy-run", handover: "variables-run")).Won.Should().BeTrue();
        (await ClaimAsync(poc.Id, "third", handover: "variables-run")).Won.Should().BeFalse("the handover names only the awaited run");
    }

    [Fact]
    public async Task Claim_TwoConcurrentClaims_OneWins_ExactlyOneQueuedRow()
    {
        var (poc, _) = await SeedAsync();

        var claims = await Task.WhenAll(ClaimAsync(poc.Id, "a"), ClaimAsync(poc.Id, "b"));

        claims.Count(c => c.Won).Should().Be(1);
        await using var db = Db();
        (await db.Runs.CountAsync(r => r.Id == "a" || r.Id == "b")).Should().Be(1);
    }

    [Fact]
    public async Task Claim_TheRowInsertFails_LeavesLastRunIdUnchanged()
    {
        var (poc, _) = await SeedAsync();
        await using (var db = Db())
        {
            db.Runs.Add(new Run { Id = "dup", Project = "poc-ann", Pipeline = "x", TicketId = "", Status = "done", FinishedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var act = () => ClaimAsync(poc.Id, "dup");

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var check = Db();
        (await check.Set<Poc>().SingleAsync(p => p.Id == poc.Id)).LastRunId.Should().BeNull();
    }

    private async Task<PocClaim> ClaimAsync(long pocId, string runId, long? revisionId = null, string? handover = null)
    {
        await using var db = Db();
        var gate = new PocRunGateRepository(db, new InitRunRepository(db, TimeProvider.System), TimeProvider.System);
        return await gate.ClaimAsync(new PocClaimRequest(pocId, runId, "poc-ann", "poc-deploy", [], "starting", StaleAfter,
            revisionId, handover), _ct);
    }

    private async Task<(Poc Poc, PocRevision Revision)> SeedAsync(string? lastRunId = null)
    {
        await using var db = Db();
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.State = PocStates.Importing;
        poc.LastRunId = lastRunId;
        await PocRows.AddAsync(db, poc);
        var revision = await new PocImportRepository(db, new AgentSmith.Infrastructure.Persistence.Services.Translators.SqliteUniqueViolationTranslator())
            .AddRevisionAsync(poc.Id, "poc-upload:x", "u1", "ann-sub", _ct);
        return (poc, revision);
    }

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);
}
