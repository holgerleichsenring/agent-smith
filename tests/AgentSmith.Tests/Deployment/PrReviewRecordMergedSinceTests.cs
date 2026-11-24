using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Deployment.DeploymentsPageSeed;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-23-8df6a: the merged pull requests the Deployments page lists — inside the scope and the window, newest
/// merge first, with their run's ticket title; a hidden project's and one whose run is gone are left out.</summary>
public sealed class PrReviewRecordMergedSinceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly DeploymentsPageSeed _seed;

    public PrReviewRecordMergedSinceTests() => _seed = new(_connection);

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task MergedSinceAsync_ScopeAndWindow_ReturnsOnlyAdmittedMergedNewestFirst()
    {
        await _seed.MergedAsync("alpha", "r1", Now.AddDays(-2));
        await _seed.MergedAsync("alpha", "r2", Now.AddDays(-1), mergeSha: null);
        await _seed.MergedAsync("alpha", "r-old", Now.AddDays(-9));
        await _seed.MergedAsync("alpha", "r-open", Now.AddDays(-1), PrReviewStates.Open);
        await _seed.MergedAsync("beta", "r-beta", Now.AddDays(-1));

        var listed = await ReadAsync(ProjectScope.Single("alpha"));

        listed.Select(m => (m.RunId, m.MergeSha)).Should().Equal(("r2", (string?)null), ("r1", "m1"));
        listed[1].Should().Match<AgentSmith.Contracts.Deployment.MergedPullRequestItem>(m =>
            m.TicketTitle == "Title r1" && m.TargetBranch == "main" && m.MergedAt == Now.AddDays(-2) && m.TicketId == "T-r1");
    }

    [Fact]
    public async Task MergedSinceAsync_HiddenProject_LeavesItOut()
    {
        await _seed.MergedAsync("alpha", "r1", Now.AddDays(-1));
        await _seed.MergedAsync(InstallationScope.Name, "r-hidden", Now.AddDays(-1));

        (await ReadAsync(ProjectScope.EverythingBut(InstallationScope.Name))).Select(m => m.Project).Should().Equal("alpha");
    }

    [Fact]
    public async Task MergedSinceAsync_RunGone_NotListed()
    {
        await _seed.MergedAsync("alpha", "r-gone", Now.AddDays(-1), withRun: false);

        (await ReadAsync(ProjectScope.Everything)).Should().BeEmpty();
    }

    private async Task<IReadOnlyList<AgentSmith.Contracts.Deployment.MergedPullRequestItem>> ReadAsync(ProjectScope scope)
    {
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        return await new PrReviewRecordRepository(ctx).MergedSinceAsync(scope, Now.AddDays(-7), default);
    }
}
