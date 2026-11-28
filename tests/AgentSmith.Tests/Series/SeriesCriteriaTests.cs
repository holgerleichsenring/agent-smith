using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Specs.Branch;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>
/// 2026-11-27-ecf2d: a merged ticket's work branch is deleted by cleanup; the series check reads its specs where it
/// landed, and a fully merged series' summary carries no "could not be read" line.
/// </summary>
public sealed class SeriesCriteriaTests
{
    private const string Key = "azuredevops-1";
    private readonly RecordingBranchSources _sources = new();
    private readonly InMemorySpecSetPointerStore _pointers = new();
    private readonly Mock<ITicketLandingStore> _landings = new();

    public SeriesCriteriaTests() =>
        _landings.Setup(l => l.LandedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    private static SeriesRunRecord Record() => SeriesRecords.Running() with { Tickets = [SeriesRecords.Ticket("1")] };

    [Fact]
    public async Task ReadAsync_TicketBranchesDeletedAfterMerge_ReadsTheCriteriaWhereTheyLanded()
    {
        await FileAsync();
        Merge();
        Landed();

        var (criteria, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        unread.Should().BeEmpty();
        criteria.Select(c => (c.SpecId, c.Text)).Should().Equal(("p9000a", "The set reads back."));
    }

    [Fact]
    public async Task ReadAsync_TicketBranchesDeletedAndNothingWhereTheyLanded_NamesNoTicket()
    {
        await FileAsync();
        _sources.Gone.Add("agent-smith/1");
        Landed();

        var (criteria, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        criteria.Should().BeEmpty();
        unread.Should().BeEmpty("a landed ticket's branch being gone is how a merge ends");
    }

    [Fact]
    public async Task ReadAsync_ABrokenSetWhereItLanded_IsNamed()
    {
        await FileAsync();
        Merge();
        _sources.Gone.Add("main");
        Landed();

        var (_, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        unread.Should().ContainSingle().Which.Should().StartWith("SCRUM-1: ").And.Contain("branch main could not be read");
    }

    [Fact]
    public async Task ReadAsync_AnUnlandedTicketsBranchGone_IsNamed()
    {
        await FileAsync();
        Merge();

        var (_, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        unread.Should().ContainSingle().Which.Should().Contain("branch agent-smith/1 could not be read").And.Contain("TF401175");
    }

    [Fact]
    public async Task Of_ASeriesWhoseTicketBranchesWereDeletedAfterMerge_HasNoErrorLines()
    {
        await FileAsync();
        Merge();
        Landed();
        var record = Record();
        var (criteria, unread) = await Criteria().ReadAsync(record, TicketBranchSeriesTests.Project(), default);
        var verdict = new SeriesVerdict("run-v", SeriesRecords.T, [.. criteria.Select(c => new SeriesCriterionResult(c.Cited, true, "ok"))])
        {
            Unread = unread,
        };
        var beat = new SeriesBeat(record, [new SeriesTicketFact(record.Tickets[0]) { LandedAt = SeriesRecords.T, CostUsd = 10m }],
            SeriesRecords.T.AddHours(4));

        var text = SeriesSummaryText.Of(beat, verdict, null);

        SeriesSummaryText.Shown(text).Should().Contain("1 of 1 hold").And.EndWith("Nothing waits on you.");
        text.Should().NotContain("could not be read").And.NotContain("Not checked").And.NotContain("TF401175");
    }

    [Fact]
    public async Task ReadAsync_ASeriesBranch_ReadsTheCriteriaThere()
    {
        await FileAsync();
        Merge("agent-smith/series/sr-1");
        Landed();

        var (criteria, unread) = await Criteria().ReadAsync(Record() with { IntegrationBranch = "agent-smith/series/sr-1" },
            TicketBranchSeriesTests.Project(), default);

        unread.Should().BeEmpty();
        criteria.Should().ContainSingle();
    }

    [Fact]
    public async Task ReadAsync_ARecordOnlyRepositoryKeptItsBranch_ReadsItThereAndTheMergedOneWhereItLanded()
    {
        await FileAsync(("p9000a", "sample-api"), ("p9000b", "sample-web"));
        foreach (var write in _sources.Writes.Where(w => w.Repo == "sample-api").ToList()) _sources.Seed(write.Repo, "main", write.Files);
        _sources.Gone.Add("sample-api:agent-smith/1");
        Landed();

        var (criteria, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        unread.Should().BeEmpty();
        criteria.Select(c => c.SpecId).Should().Equal("p9000a", "p9000b");
    }

    [Fact]
    public async Task ReadAsync_ATicketLandedByHand_IsNamed()
    {
        await FileAsync();
        _sources.Gone.Add("agent-smith/1");
        Landed(TicketLandingKinds.ByHand);

        var (_, unread) = await Criteria().ReadAsync(Record(), TicketBranchSeriesTests.Project(), default);

        unread.Should().ContainSingle().Which.Should().Contain("branch agent-smith/1 could not be read");
    }

    private SeriesCriteria Criteria()
    {
        var services = new ServiceCollection().AddSingleton(_landings.Object).BuildServiceProvider();
        return new SeriesCriteria(TicketBranchSeriesTests.Reader(_sources, _pointers), services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SeriesCriteria>.Instance);
    }

    // The ticket's set, filed onto its work branch as filing writes it.
    private async Task FileAsync(params (string Id, string Repo)[] specs)
    {
        var record = new SpecApprovalRecord(Key,
            new SpecSet(Key, TicketBranchSeriesTests.Specs(specs.Length > 0 ? specs : [("p9000a", "sample-api")]), SpecAccounting.Empty, [],
                SpecSource.Approved, Approval: new SpecApproval(DateTimeOffset.UnixEpoch, "job-1", "sample.user"), Series: "2026-11-17-0b0b"),
            ["sample-api", "sample-web"], "sample-tracker", "sample-api");
        var written = await ApprovedSetDoubles.Branch(_sources, _pointers)
            .WriteAsync(TicketBranchSeriesTests.Project(), record, new TicketId("1"), TicketBranchSeriesTests.Stored(), default);
        written.Written.Should().BeTrue(written.Error);
    }

    // The pull request merged into the target (main by default), and cleanup deleted the work branch.
    private void Merge(string into = "main")
    {
        foreach (var write in _sources.Writes.ToList()) _sources.Seed(write.Repo, into, write.Files);
        _sources.Gone.Add("agent-smith/1");
    }

    private void Landed(string kind = TicketLandingKinds.Merged) =>
        _landings.Setup(l => l.LandedAsync("sample-tracker", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketLandingFact("sample-tracker", Key, "1", "proj", kind, SeriesRecords.T, null, null, null)]);
}
