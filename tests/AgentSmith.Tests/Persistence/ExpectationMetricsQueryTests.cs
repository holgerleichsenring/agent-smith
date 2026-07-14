using AgentSmith.Contracts.Expectations;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0329: the metric's backing query + aggregation — ratification outcomes
/// joined to runs by RunId, grouped per project with month buckets.
/// Definitions under test: expectation-hit-rate = verbatim / human-ratified
/// (verbatim+edited+rejected; null before any human ratification) and
/// first-PR-acceptance = (verbatim+edited) / all negotiated runs — both
/// derived ONLY from p0328 ratification outcomes.
/// </summary>
public sealed class ExpectationMetricsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ExpectationMetricsQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task MetricsQuery_RatificationOutcomes_AggregatesCorrectly()
    {
        SeedAlphaAndBeta();

        using var ctx = new AgentSmithDbContext(Options());
        var rows = await new ExpectationMetricsRepository(ctx)
            .GetOutcomeRowsAsync(CancellationToken.None);
        var snapshot = ExpectationMetricsAggregator.Aggregate(rows);

        snapshot.Total.Should().Be(6, "the orphan expectation without a run row is excluded");
        var alpha = snapshot.Projects.Should().HaveCount(2).And.Subject.First();
        alpha.Project.Should().Be("alpha");
        alpha.Counts.Should().Be(new ExpectationMetricsSnapshot.OutcomeCounts(5, 1, 2, 1, 1));
        alpha.ExpectationHitRate.Should().Be(0.25, "1 verbatim of 4 human-ratified");
        alpha.FirstPrAcceptance.Should().Be(0.6, "3 accepted contracts of 5 negotiated runs");
        alpha.AverageEditDistance.Should().Be(8, "mean of the edited outcomes 12 and 4");
        alpha.Months.Select(m => m.Month).Should().Equal("2026-05", "2026-06");
        alpha.Months[0].Counts.Total.Should().Be(2);

        var beta = snapshot.Projects.Last();
        beta.ExpectationHitRate.Should().BeNull("nobody has ratified anything for beta yet");
        beta.FirstPrAcceptance.Should().Be(0, "an unratified auto-stamp is not an accepted contract");
    }

    private void SeedAlphaAndBeta()
    {
        using var ctx = new AgentSmithDbContext(Options());
        var may = DateTimeOffset.Parse("2026-05-10T10:00:00Z");
        var june = DateTimeOffset.Parse("2026-06-10T10:00:00Z");
        Seed(ctx, "run-1", "alpha", ExpectationOutcomes.Verbatim, 0, may);
        Seed(ctx, "run-2", "alpha", ExpectationOutcomes.Edited, 12, may);
        Seed(ctx, "run-3", "alpha", ExpectationOutcomes.Edited, 4, june);
        Seed(ctx, "run-4", "alpha", ExpectationOutcomes.Rejected, 0, june);
        Seed(ctx, "run-5", "alpha", ExpectationOutcomes.Unratified, 0, june);
        Seed(ctx, "run-6", "beta", ExpectationOutcomes.Unratified, 0, june);
        ctx.RunExpectations.Add(new RunExpectation
        {
            RunId = "run-orphan", Outcome = ExpectationOutcomes.Verbatim, RatifiedAt = june,
        });
        ctx.SaveChanges();
    }

    private static void Seed(
        AgentSmithDbContext ctx, string runId, string project,
        string outcome, int editDistance, DateTimeOffset ratifiedAt)
    {
        ctx.Runs.Add(new Run { Id = runId, Project = project, Status = "success", StartedAt = ratifiedAt });
        ctx.RunExpectations.Add(new RunExpectation
        {
            RunId = runId, Outcome = outcome, EditDistance = editDistance, RatifiedAt = ratifiedAt,
        });
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
