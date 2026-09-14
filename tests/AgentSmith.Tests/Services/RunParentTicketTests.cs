using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-13-5cdf: the parent stamp 2026-09-13-a72a puts on an epic child reaches BOTH
/// consumers of the base ladder from one read — the branch the run cuts and the basis its
/// delivery is accounted against. Two readers would be two chances to disagree.
/// </summary>
public sealed class RunParentTicketTests
{
    private static PipelineContext WithTicket(params string[] labels)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("19106"));
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("19106"), "title", "description", null, "Open", "test", labels));
        return pipeline;
    }

    [Fact]
    public void RunParentTicket_TheChildCarriesItsParentStamp_IsRead()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));

        RunParentTicket.Of(pipeline).Should().Be("4711");
    }

    [Fact]
    public void RunParentTicket_ATicketThatIsNotASlice_NamesNoParent()
    {
        RunParentTicket.Of(WithTicket(FiledTicketLabels.PredecessorStamp("4712"))).Should()
            .BeNull("a predecessor is a sibling, not an ancestor — cutting from it would be wrong");
        RunParentTicket.Of(new PipelineContext()).Should()
            .BeNull("a run with no ticket at all has no place in a cut");
    }

    [Fact]
    public void RunBranch_CarriesTheParentStamp_WithoutNamingItInTheBranch()
    {
        var branch = RunBranchResolver.Resolve(WithTicket(FiledTicketLabels.ParentStamp("4711")));

        branch.Should().NotBeNull();
        branch!.ParentTicketId.Should().Be("4711");
        branch.Name.Value.Should().Be("agent-smith/19106",
            "a feature segment in the name would abandon the open pull request of every "
            + "ticket that already has one");
    }

    [Fact]
    public void DeliveryBasis_ReadsTheSameStampTheCutDid()
    {
        var pipeline = WithTicket(FiledTicketLabels.ParentStamp("4711"));
        pipeline.Set(ContextKeys.RunId, "run-5cdf");

        var basis = DeliveryBasis.For(pipeline);

        basis.RunId.Should().Be("run-5cdf");
        basis.ParentTicketId.Should().Be(RunBranchResolver.Resolve(pipeline)!.ParentTicketId,
            "the account resolves the base the cut used, not a base of its own");
    }
}
