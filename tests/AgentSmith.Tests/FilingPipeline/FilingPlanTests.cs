using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Tests.SpecDialog.TicketGraph;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.FilingPipeline;

/// <summary>2026-11-17-5701b: the approved proposal as the ordered tickets a 'file' run creates.</summary>
public sealed class FilingPlanTests
{
    private readonly TicketGraphRig _rig = new();

    [Fact]
    public async Task Plan_Spec_OneStampedTicketOfOneSpec()
    {
        var plan = await PlanAsync(new SpecOutcome(SpecOf("api")));

        var ticket = plan.Tickets.Should().ContainSingle().Subject;
        ticket.Role.Should().Be(TicketFilingRole.Spec);
        ticket.Series!.Drafts.Should().ContainSingle();
        ticket.Labels.Should().Equal(ApprovedSetTicketFiler.Labels(_rig.Project));
        ticket.Requires.Should().BeEmpty();
    }

    [Fact]
    public async Task Plan_EpicLongerThanASeries_PartsRequireTheOneBefore()
    {
        var epic = new EpicOutcome(SpecOf("parent"), [SpecOf("a"), SpecOf("b"), SpecOf("c")]);

        var plan = await PlanAsync(epic, new SeriesConfig { MaxSpecs = 2 });

        plan.Tickets.Select(t => (t.Key, t.Goal, t.Series!.Drafts.Count)).Should().Equal(
            ("epic part 1", "Goal of parent (part 1 of 2)", 2), ("epic part 2", "Goal of parent (part 2 of 2)", 1));
        plan.Tickets[1].Requires.Should().ContainSingle().Which.Should().Be("epic part 1", "a later part starts through its requires edge");
        plan.Tickets.Should().OnlyContain(t => t.Role == TicketFilingRole.Work);
    }

    [Fact]
    public async Task Plan_ListWithSpecs_OneTicketEachInMergeOrderWithItsRepos()
    {
        var plan = await PlanAsync(List(Ticket("ui", "api"), Ticket("api") with { Repos = ["sample-api"] }));

        plan.Tickets.Select(t => (t.Key, t.Requires.Count)).Should().Equal(("api", 0), ("ui", 1));
        plan.Tickets[0].Repos.Should().Equal("sample-api");
        plan.Tickets[1].Requires.Should().Equal("api");
        plan.Tickets.Should().OnlyContain(t => t.Series != null && t.Series.Drafts.Count == 1);
    }

    [Fact]
    public async Task Plan_ListTicketWithMoreSpecsThanASeries_SplitAndItsDependentRequiresTheLastPart()
    {
        var big = Ticket("api") with { Specs = [SpecOf("a"), SpecOf("b"), SpecOf("c")] };

        var plan = await PlanAsync(List(big, Ticket("ui", "api")), new SeriesConfig { MaxSpecs = 2 });

        plan.Tickets.Select(t => t.Key).Should().Equal("api part 1", "api part 2", "ui");
        plan.Tickets[1].Requires.Should().Equal("api part 1");
        plan.Tickets[2].Requires.Should().Equal("api part 2");
        plan.Tickets[0].Render([]).Title.Should().Be("Title api (part 1 of 2)");
    }

    [Fact]
    public async Task Plan_Bug_OneTicketWithNoSeriesAndNoLabel()
    {
        var plan = await PlanAsync(new BugOutcome(new BugTicketDraft("Session drops", "Users are signed out.", null)));

        var bug = plan.Tickets.Should().ContainSingle().Subject;
        (bug.Series, bug.Labels.Count, bug.Role).Should().Be((null, 0, TicketFilingRole.Bug));
    }

    [Fact]
    public async Task Plan_ListTicketWithoutASpec_RefusesTheWholeListNamingIt()
    {
        var act = () => PlanAsync(List(Ticket("api"), Ticket("ui", "api") with { Specs = [] }));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message
            .Should().Contain("ticket 'ui'").And.Contain("draft:tickets/ui/<label>.yaml");
    }

    [Fact]
    public async Task Plan_RequiresCycle_Refused()
    {
        var act = () => PlanAsync(List(Ticket("api", "ui"), Ticket("ui", "api")));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().StartWith("The list cannot be filed");
    }

    [Fact]
    public async Task Plan_RequiredTicketTheTrackerLacks_RefusedBeforeAnythingExists()
    {
        var act = () => PlanAsync(List(Ticket("api", "ticket:77")));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("ticket 77 does not exist");
        _rig.Tracker.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task Plan_FollowUpOfAPersonsAct_OneWorkTicketWithNoSeriesAndNoLabel()
    {
        var request = new FilingRequest(string.Empty, "carol", [], [], Proposal: null, MayStartRuns: true)
            { FollowUp = new FilingFollowUp("Follow-up: Widget drops", "also cover the archive view") };

        var plan = await FilingDoubles.Planner(Mock.Of<ITicketProviderFactory>()).PlanAsync(request, _rig.Project, CancellationToken.None);

        var ticket = plan.Tickets.Should().ContainSingle().Subject;
        (ticket.Role, ticket.Series, ticket.Labels.Count).Should().Be((AgentSmith.Contracts.Tickets.TicketFilingRole.Work, null, 0));
        ticket.Render([]).Body.Should().Be("also cover the archive view");
    }

    [Fact]
    public async Task Plan_NeitherProposalNorFollowUp_IsRefused()
    {
        var request = new FilingRequest(string.Empty, "carol", [], [], Proposal: null, MayStartRuns: true);

        var act = () => FilingDoubles.Planner(Mock.Of<ITicketProviderFactory>()).PlanAsync(request, _rig.Project, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("neither a proposal nor a follow-up");
    }

    private Task<FilingPlan> PlanAsync(OutcomeProposal proposal, SeriesConfig? settings = null)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_rig.Tracker);
        var request = new FilingRequest(ConversationId, "sample.user", [], [], proposal, MayStartRuns: true);
        return FilingDoubles.Planner(factory.Object, settings: settings).PlanAsync(request, _rig.Project, CancellationToken.None);
    }
}
