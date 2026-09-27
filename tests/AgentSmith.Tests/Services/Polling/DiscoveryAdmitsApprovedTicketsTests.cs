using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Polling;

/// <summary>
/// 2026-09-25-c1f7: a ticket whose approval stamp somebody deleted is still FETCHED. Until this
/// phase the two trackers that filter server-side pushed the label guard into JQL and WIQL, so
/// such a ticket was never fetched at all and nothing downstream — including the record-reading
/// bind of 2026-09-25-3c7aa — ever saw it.
/// </summary>
public sealed class DiscoveryAdmitsApprovedTicketsTests
{
    private const string TrackerName = "jira-main";

    [Fact]
    public async Task Discovery_AnApprovedTicketWithoutTheStamp_IsAdmittedByItsId()
    {
        var store = await StoreWithAsync(("jira-dpg-1239", "DPG-1239"));

        var query = await BuildAsync(store);

        query.ApprovedTicketIds.Should().Equal("DPG-1239");
        new JiraDiscoveryJqlBuilder().BuildJql(query).Should().Contain("key IN (\"DPG-1239\")",
            "the label guard excludes this ticket, so only a clause beside it can admit it");
    }

    [Fact]
    public async Task Discovery_ASatisfiedRecord_IsNotAdmittedAgain()
    {
        var store = await StoreWithAsync(("jira-dpg-1239", "DPG-1239"), ("jira-dpg-9", "DPG-9"));
        await store.MarkSatisfiedAsync(
            TrackerName, "jira-dpg-1239", ApprovedSets.Noon, CancellationToken.None);

        (await BuildAsync(store)).ApprovedTicketIds.Should().BeEquivalentTo(["DPG-9"],
            "a run finished DPG-1239, and the set would otherwise grow with every approval forever");
    }

    [Fact]
    public async Task Discovery_MoreOutstandingRecordsThanTheCap_NamesTheOldestAndReportsTheRest()
    {
        var store = new InMemorySpecApprovalStore();
        for (var i = 0; i < ApprovedTicketAdmission.MaxTicketIds + 3; i++)
            await store.SaveAsync(
                ApprovedSets.Record($"jira-t{i}", ApprovedSets.Noon.AddMinutes(i),
                    tracker: TrackerName, ticketId: $"T-{i}"),
                CancellationToken.None);

        var outstanding = await store.ListOutstandingAsync(
            TrackerName, ApprovedTicketAdmission.MaxTicketIds, CancellationToken.None);

        outstanding.TicketIds.Should().HaveCount(ApprovedTicketAdmission.MaxTicketIds)
            .And.StartWith(["T-0", "T-1"], "the oldest approvals are the ones named");
        outstanding.Omitted.Should().Be(3, "what did not fit is reported, never silently dropped");
        (await BuildAsync(store)).ApprovedTicketIds.Should()
            .HaveCount(ApprovedTicketAdmission.MaxTicketIds);
    }

    [Fact]
    public async Task Discovery_TheJqlAndWiql_OrTheIdsAtTheTopLevel()
    {
        var query = await BuildAsync(await StoreWithAsync(("jira-dpg-1239", "1239")));

        var jql = new JiraDiscoveryJqlBuilder().BuildJql(query);
        var wiql = new AzureDevOpsDiscoveryWiqlBuilder().BuildWhere(query, ["Active"]);

        jql.Should().EndWith(") OR key IN (\"1239\")").And.Contain("AND labels IN (",
            "the guard stays AND-ed onto the routing clause; the ids are OR'd with the whole query");
        wiql.Should().EndWith(") OR [System.Id] IN (1239)").And.Contain("[System.Tags] CONTAINS");
    }

    /// <summary>
    /// GitHub and GitLab narrow by the branch's own tag or fall back to listing open issues and
    /// never read <see cref="DiscoveryQuery.TriggerLabels"/> at all, so the stamp is not in their
    /// filter and there is nothing there for the ids to widen. Stated as a test so a later reader
    /// does not add the clause for symmetry.
    /// </summary>
    [Fact]
    public async Task Discovery_GitHubAndGitLab_AreUnchanged()
    {
        var query = await BuildAsync(await StoreWithAsync(("jira-dpg-1239", "DPG-1239")));

        query.AllTagLabelsOrNull().Should().BeEquivalentTo(["alpha-tag"],
            "what those two providers read is the branch criterion, which this phase never touched");
    }

    /// <summary>
    /// The stored id is the TRACKER'S OWN. <c>SpecSetKey.For</c> lowercases it and replaces every
    /// non-alphanumeric character, so a query built from the key would ask Jira for
    /// <c>jira-dpg-1239</c> and match nothing — and recovering the id with a per-provider parser is
    /// what this repository refused to write for the ticket's label stamp.
    /// </summary>
    [Fact]
    public async Task SpecKey_TheStoredTicketId_IsTheTrackersOwnAndNotDerivedFromTheKey()
    {
        var key = SpecSetKey.For("jira", "DPG-1239");
        var store = await StoreWithAsync((key.Value, "DPG-1239"));

        var outstanding = await store.ListOutstandingAsync(TrackerName, 10, CancellationToken.None);

        key.Value.Should().Be("jira-dpg-1239");
        outstanding.TicketIds.Should().Equal("DPG-1239");
    }

    [Fact]
    public async Task Discovery_ARecordWithNoTicketId_IsNeverNamed()
    {
        var store = new InMemorySpecApprovalStore();
        await store.SaveAsync(
            ApprovedSets.Record("jira-dpg-1239", ApprovedSets.Noon, tracker: TrackerName),
            CancellationToken.None);

        (await BuildAsync(store)).ApprovedTicketIds.Should().BeEmpty(
            "a row written before the column exists cannot be named, and a guess would fetch the "
            + "wrong ticket rather than none");
    }

    [Fact]
    public async Task Discovery_NoStore_LeavesTheQueryExactlyAsItWas()
    {
        var builder = new TrackerDiscoveryQueryBuilder(
            NullLogger<TrackerDiscoveryQueryBuilder>.Instance);

        var query = await builder.BuildAsync(Config(), Tracker(), CancellationToken.None);

        query.ApprovedTicketIds.Should().BeEmpty();
        new JiraDiscoveryJqlBuilder().BuildJql(query).Should().NotContain("key IN (");
    }

    /// <summary>
    /// The builder takes the admission OPTIONALLY, so a registration that never landed would show
    /// up as a query that quietly stopped admitting anything. Resolved from the real container.
    /// </summary>
    [Fact]
    public async Task Discovery_TheComposedGraph_HandsTheBuilderItsAdmission()
    {
        var store = await StoreWithAsync(("jira-dpg-1239", "DPG-1239"));
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ISpecApprovalStore>(store)
            .AddSpecDerivation()
            .AddTransient<ITrackerDiscoveryQueryBuilder, TrackerDiscoveryQueryBuilder>()
            .BuildServiceProvider();

        var query = await provider.GetRequiredService<ITrackerDiscoveryQueryBuilder>()
            .BuildAsync(Config(), Tracker(), CancellationToken.None);

        query.ApprovedTicketIds.Should().Equal("DPG-1239");
    }

    private static async Task<InMemorySpecApprovalStore> StoreWithAsync(
        params (string Key, string TicketId)[] records)
    {
        var store = new InMemorySpecApprovalStore();
        foreach (var (key, ticketId) in records)
            await store.SaveAsync(
                ApprovedSets.Record(key, ApprovedSets.Noon, tracker: TrackerName, ticketId: ticketId),
                CancellationToken.None);
        return store;
    }

    private static Task<DiscoveryQuery> BuildAsync(ISpecApprovalStore store) =>
        new TrackerDiscoveryQueryBuilder(
                NullLogger<TrackerDiscoveryQueryBuilder>.Instance,
                findings: null,
                new ApprovedTicketAdmission(store, NullLogger<ApprovedTicketAdmission>.Instance))
            .BuildAsync(Config(), Tracker(), CancellationToken.None);

    private static TrackerConnection Tracker() =>
        new() { Name = TrackerName, Type = TrackerType.Jira };

    /// <summary>One label-gated project, which is what turns the server-side guard on.</summary>
    private static AgentSmithConfig Config()
    {
        var tracker = Tracker();
        var project = new ResolvedProject
        {
            Name = "alpha",
            Tracker = tracker,
            JiraTrigger = new JiraTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.Tag,
                    Value = "alpha-tag",
                },
                TriggerStatuses = ["To Do"],
                PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug" },
            },
        };
        return new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { [tracker.Name] = tracker },
            Projects = new Dictionary<string, ResolvedProject> { [project.Name] = project },
        };
    }
}
