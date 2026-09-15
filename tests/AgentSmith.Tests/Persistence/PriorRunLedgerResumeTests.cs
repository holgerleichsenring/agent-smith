using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Tests.TestSupport;
using System;
using System.Collections.Generic;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0356: the resume-after-reap loop, proven on a real SQLite engine — a
/// mid-run RunStoryRecorded flush persists the ledger (Note included) on the
/// run row; DbPriorRunLedgerReader returns the LATEST ledger for the PROJECT
/// AND ticket (2026-09-09-b26f); the seeder turns it into the next run's
/// opening checklist.
/// </summary>
public sealed class PriorRunLedgerResumeTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PriorRunLedgerResumeTests()
    {
        _connection = MigratedStoreTemplate.OpenCopy();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<DbPriorRunLedgerReader>();
        return services.BuildServiceProvider();
    }

    private static string LedgerJson(params ProgressLedgerEntry[] entries) =>
        Application.Services.RunStorySnapshotBuilder.BuildLedgerJson(new ProgressLedger(entries))!;

    private async Task ProjectAsync(ServiceProvider provider, RunEvent ev)
    {
        var applier = provider.GetRequiredService<RunEventApplier>();
        using var scope = provider.CreateScope();
        await applier.ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, CancellationToken.None);
    }

    private const string Project = "alpha";

    private Task StartRunAsync(
        ServiceProvider provider, string runId, string ticketId, DateTimeOffset at,
        string project = Project) =>
        ProjectAsync(provider, new RunStartedEvent(
            runId, "ticket", "fix-bug", new[] { "primary" }, at, "claude", ticketId,
            Project: project));

    [Fact]
    public async Task Ledger_FlushedMidRun_NotePersisted_ResumeSeedsFromLatestSameTicketRun()
    {
        var provider = BuildProvider();
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);
        // Run ids are time-sortable by construction — the reader keys "latest" on them.
        const string olderRun = "2026-07-20T0700-aaaa";
        const string reapedRun = "2026-07-20T0800-bbbb";
        const string otherTicketRun = "2026-07-20T0900-cccc";
        await StartRunAsync(provider, olderRun, "77", t0.AddHours(-1));
        await StartRunAsync(provider, reapedRun, "77", t0);
        await StartRunAsync(provider, otherTicketRun, "99", t0.AddMinutes(30));

        // Mid-run flushes: the older run finished a different shape; the reaped
        // run flushed batches + a convention note before it died.
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            olderRun, LedgerJson(new ProgressLedgerEntry("1", "old shape", ProgressStatus.Done)),
            null, t0.AddMinutes(-30)));
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            reapedRun,
            LedgerJson(
                new ProgressLedgerEntry("1", "batch: representative sites", ProgressStatus.Done,
                    Target: "src/A.cs", Note: "convention: replace via factory"),
                new ProgressLedgerEntry("2", "batch: remaining call sites (23)", ProgressStatus.InProgress)),
            null, t0.AddMinutes(10)));
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            otherTicketRun, LedgerJson(new ProgressLedgerEntry("1", "unrelated", ProgressStatus.Done)),
            null, t0.AddMinutes(40)));

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync(Project, "77", CancellationToken.None);

        prior.Should().NotBeNull();
        prior!.RunId.Should().Be(reapedRun, "the LATEST same-ticket run wins");
        var seed = PriorRunLedgerSeeder.Seed(prior, DateTimeOffset.UtcNow);
        seed.Should().HaveCount(2);
        seed[0].Note.Should().Be("convention: replace via factory", "the note survives the round trip");
        seed[1].Status.Should().Be(ProgressStatus.Pending, "the interrupted step is re-verified");
    }

    [Fact]
    public async Task ReadLatestForTicket_NoLedgerRows_Null()
    {
        var provider = BuildProvider();
        await StartRunAsync(provider, "run-1", "77", DateTimeOffset.UtcNow);

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync(Project, "77", CancellationToken.None);

        prior.Should().BeNull();
    }

    [Fact]
    public async Task ReadLatestForTicket_CorruptLedgerJson_NullNeverThrows()
    {
        var provider = BuildProvider();
        await StartRunAsync(provider, "run-1", "77", DateTimeOffset.UtcNow);
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            var run = await ctx.Set<global::AgentSmith.Infrastructure.Persistence.Entities.Run>()
                .FirstAsync(r => r.Id == "run-1");
            run.ProgressLedgerJson = "{not json";
            await ctx.SaveChangesAsync();
        }

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync(Project, "77", CancellationToken.None);

        prior.Should().BeNull();
    }

    // 2026-09-09-b26f: the defect this phase closes. Two projects on one tracker
    // both carry ticket 4471; keyed on the ticket alone, the newer run's ledger
    // seeds the other project's checklist.
    [Fact]
    public async Task PriorLedger_TwoProjectsOneTicketId_EachSeesOnlyItsOwn()
    {
        var provider = BuildProvider();
        var t0 = DateTimeOffset.UtcNow.AddHours(-1);
        const string alphaRun = "2026-09-09T0700-aaaa";
        // Sorts ABOVE alpha's run by id, so an unkeyed read returns THIS one for both.
        const string betaRun = "2026-09-09T0800-bbbb";
        await StartRunAsync(provider, alphaRun, "4471", t0, "alpha");
        await StartRunAsync(provider, betaRun, "4471", t0.AddMinutes(30), "beta");
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            alphaRun, LedgerJson(new ProgressLedgerEntry("1", "alpha work", ProgressStatus.Done)),
            null, t0.AddMinutes(10)));
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            betaRun, LedgerJson(new ProgressLedgerEntry("1", "beta work", ProgressStatus.Done)),
            null, t0.AddMinutes(40)));

        var reader = provider.GetRequiredService<DbPriorRunLedgerReader>();
        var alpha = await reader.ReadLatestForTicketAsync("alpha", "4471", CancellationToken.None);
        var beta = await reader.ReadLatestForTicketAsync("beta", "4471", CancellationToken.None);

        alpha.Should().NotBeNull();
        alpha!.RunId.Should().Be(alphaRun, "the newer beta run must not seed alpha");
        alpha.Items.Should().ContainSingle().Which.Activity.Should().Be("alpha work");
        beta.Should().NotBeNull();
        beta!.RunId.Should().Be(betaRun);
        beta.Items.Should().ContainSingle().Which.Activity.Should().Be("beta work");
    }

    [Fact]
    public async Task PriorLedger_SameProjectTwoRuns_SeesTheNewer()
    {
        var provider = BuildProvider();
        var t0 = DateTimeOffset.UtcNow.AddHours(-1);
        const string older = "2026-09-09T0700-aaaa";
        const string newer = "2026-09-09T0800-bbbb";
        await StartRunAsync(provider, older, "4471", t0);
        await StartRunAsync(provider, newer, "4471", t0.AddMinutes(30));
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            older, LedgerJson(new ProgressLedgerEntry("1", "first attempt", ProgressStatus.Done)),
            null, t0.AddMinutes(10)));
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            newer, LedgerJson(new ProgressLedgerEntry("1", "second attempt", ProgressStatus.Done)),
            null, t0.AddMinutes(40)));

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync(Project, "4471", CancellationToken.None);

        prior.Should().NotBeNull();
        prior!.RunId.Should().Be(newer, "keying on the project did not cost the latest-run ordering");
    }

    [Fact]
    public async Task PriorLedger_NoRunForThisProject_IsNull()
    {
        var provider = BuildProvider();
        var t0 = DateTimeOffset.UtcNow.AddHours(-1);
        await StartRunAsync(provider, "2026-09-09T0700-aaaa", "4471", t0, "alpha");
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            "2026-09-09T0700-aaaa",
            LedgerJson(new ProgressLedgerEntry("1", "alpha work", ProgressStatus.Done)),
            null, t0.AddMinutes(10)));

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync("gamma", "4471", CancellationToken.None);

        prior.Should().BeNull("a project with no run of its own starts empty, not on someone else's");
    }

    // An ephemeral composition resolves no project name; the reader reads
    // nothing rather than falling back to the ticket alone.
    [Fact]
    public async Task PriorLedger_BlankProject_IsNull()
    {
        var provider = BuildProvider();
        var t0 = DateTimeOffset.UtcNow.AddHours(-1);
        await StartRunAsync(provider, "2026-09-09T0700-aaaa", "4471", t0, "alpha");
        await ProjectAsync(provider, new RunStoryRecordedEvent(
            "2026-09-09T0700-aaaa",
            LedgerJson(new ProgressLedgerEntry("1", "alpha work", ProgressStatus.Done)),
            null, t0.AddMinutes(10)));

        var prior = await provider.GetRequiredService<DbPriorRunLedgerReader>()
            .ReadLatestForTicketAsync("  ", "4471", CancellationToken.None);

        prior.Should().BeNull();
    }
}
