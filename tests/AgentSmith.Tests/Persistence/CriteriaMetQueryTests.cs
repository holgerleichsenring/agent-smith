using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// Criteria met over a real migrated store: finished coding runs' acceptance snapshots,
/// counted by status with the operator's overrules applied, per project and month.
/// </summary>
public sealed class CriteriaMetQueryTests : IDisposable
{
    private static readonly DateTimeOffset April = DateTimeOffset.Parse("2026-04-15T10:00:00Z");
    private static readonly DateTimeOffset MayInAnotherZone = DateTimeOffset.Parse("2026-05-31T23:30:00-02:00");
    private static readonly DateTimeOffset June = DateTimeOffset.Parse("2026-06-10T10:00:00Z");
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ExpectationMetrics_AggregatesFromAcceptanceSnapshot_PerProjectPerMonth()
    {
        SeedRun("run-0", "alpha", April, [Criterion("z", "met")]);
        SeedRun("run-1", "alpha", MayInAnotherZone, [Criterion("a", "met"), Criterion("b", "unmet")]);
        SeedRun("run-2", "alpha", June, [Criterion("c", "met")]);
        SeedRun("run-3", "beta", June, [Criterion("d", "unmet")]);
        SeedRun("run-open", "alpha", finishedAt: null, [Criterion("e", "unmet")]);

        var snapshot = await ReadAsync();

        snapshot.Runs.Should().Be(4, "an unfinished run has not been judged yet");
        snapshot.Counts.Should().Be(new CriterionCounts(3, 2, 0, 0, 0, 0));
        var alpha = snapshot.Projects.Should().HaveCount(2).And.Subject.First();
        alpha.Project.Should().Be("alpha");
        alpha.Counts.Share.Should().Be(0.75);
        alpha.Months.Select(m => m.Month).Should().Equal(["2026-04", "2026-06"],
            "May 31st 23:30 at -02:00 finished on June 1st in UTC");
        alpha.Months[1].Runs.Should().Be(2);
        alpha.Months[1].Counts.Share.Should().BeApproximately(2.0 / 3, 1e-9);
        snapshot.Projects.Last().Counts.Share.Should().Be(0);
    }

    [Fact]
    public async Task ExpectationMetrics_NotApplicableExcluded_UnprovenCountsAgainst()
    {
        SeedRun("run-1", "alpha", June,
            [Criterion("a", "met"), Criterion("b", "unproven"), Criterion("c", "not_applicable")],
            source: AcceptanceSources.MasterVerification);
        SeedRun("run-2", "alpha", June, [Criterion("d", "met"), Criterion("repo-key", "unproven")]);

        var counts = (await ReadAsync()).Counts;

        counts.Should().Be(new CriterionCounts(2, 0, 1, 1, 0, 0),
            "the delivery account's per-repository row names a repository, not a criterion");
        counts.Judged.Should().Be(3);
        counts.Share.Should().BeApproximately(2.0 / 3, 1e-9);
    }

    [Fact]
    public async Task ExpectationMetrics_OverruleOnCurrentStatus_Counts()
    {
        SeedRun("run-1", "alpha", June, [Criterion("a", "unmet"), Criterion("b", "met")]);
        SeedJudgement("run-1", "a", machineStatus: "unmet", humanStatus: "met");

        var counts = (await ReadAsync()).Counts;

        counts.Should().Be(new CriterionCounts(2, 0, 0, 0, 1, 0));
        counts.Share.Should().Be(1);
    }

    [Fact]
    public async Task ExpectationMetrics_StaleOverrule_NotCounted()
    {
        SeedRun("run-1", "alpha", June, [Criterion("a", "met"), Criterion("b", "unmet")]);
        SeedJudgement("run-1", "a", machineStatus: "unmet", humanStatus: "not_applicable");

        var counts = (await ReadAsync()).Counts;

        counts.Should().Be(new CriterionCounts(1, 1, 0, 0, 0, 1),
            "the judgement answered a status the snapshot no longer carries");
    }

    [Fact]
    public async Task ExpectationMetrics_ScanRuns_NotCounted()
    {
        SeedRun("run-1", "alpha", June, [Criterion("a", "met")]);
        SeedRun("run-2", "alpha", June, [Criterion("b", "unmet")], pipeline: "security-scan");

        var snapshot = await ReadAsync();

        snapshot.Runs.Should().Be(1);
        snapshot.Counts.Should().Be(new CriterionCounts(1, 0, 0, 0, 0, 0));
    }

    private async Task<CriteriaMetSnapshot> ReadAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        var runs = await new CriteriaMetRepository(ctx).GetJudgedCodeRunsAsync(CancellationToken.None);
        return new CriteriaMetAggregator(new RunCriteriaCounter()).Aggregate(runs);
    }

    private static AcceptanceCriterionView Criterion(string text, string status) => new(text, status, null);

    private void SeedRun(string id, string project, DateTimeOffset? finishedAt,
        AcceptanceCriterionView[] criteria, string source = AcceptanceSources.DeliveryAccount,
        string pipeline = PipelinePresets.CodeName)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = id, Project = project, Pipeline = pipeline, Status = "success",
            StartedAt = June.AddDays(-30), FinishedAt = finishedAt,
            AcceptanceJson = RunStoryJson.Serialize(new AcceptanceView(criteria, "verbatim", "spec", source)),
        });
        ctx.SaveChanges();
    }

    private void SeedJudgement(string runId, string criterion, string machineStatus, string humanStatus)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Set<RunCriterionJudgement>().Add(new RunCriterionJudgement
        {
            RunId = runId, CriterionKey = CriterionKey.Of(criterion), CriterionText = criterion,
            MachineStatus = machineStatus, HumanStatus = humanStatus, Reason = "checked by hand",
            Author = "operator", RecordedAt = June,
        });
        ctx.SaveChanges();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
