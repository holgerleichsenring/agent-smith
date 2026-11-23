using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Models;

/// <summary>
/// 2026-10-12-6bfaa: the branches a run's context read tries before the default branch are the
/// branches its checkout would land on — the run branch, then the parent's rung.
/// </summary>
public sealed class ContextReadBranchesTests
{
    [Fact]
    public void For_ATicketRun_TriesTheComposedTicketBranch()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("4711"));

        ContextReadBranches.For(pipeline, "api").Candidates.Should().Equal(new BranchName("agent-smith/4711"));
    }

    [Fact]
    public void For_AHandedBranch_TriesThatBranch()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("4711"));
        pipeline.Set(ContextKeys.CheckoutBranch, "feature/review-me");

        ContextReadBranches.For(pipeline, "api").Candidates.Should().Equal(new BranchName("feature/review-me"));
    }

    [Fact]
    public void For_ASlice_TriesItsParentsRungSecond()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("4712"));
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("4712"), "t", "d", null, "open", "test",
            [FiledTicketLabels.ParentStamp("4700")]));

        ContextReadBranches.For(pipeline, "api").Candidates.Should()
            .Equal(new BranchName("agent-smith/4712"), new BranchName("agent-smith/4700"));
    }

    // 2026-11-23-31dca: a stacked run reads each repo at that repo's own prerequisite branch.
    [Fact]
    public void For_AStackedRun_TriesTheRepositorysOwnRung()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("4712"));
        pipeline.Set<object>(ContextKeys.StackedOnTicket, new Dictionary<string, string> { ["server"] = "4701" });

        ContextReadBranches.For(pipeline, "server").Candidates.Should()
            .Equal(new BranchName("agent-smith/4712"), new BranchName("agent-smith/4701"));
        ContextReadBranches.For(pipeline, "client").Candidates.Should().Equal(new BranchName("agent-smith/4712"));
    }

    [Fact]
    public void For_NoTicketNoBranch_IsNone_AndForBranchIsThatBranch()
    {
        ContextReadBranches.For(new PipelineContext(), "api").Should().BeSameAs(ContextReadBranches.None);
        ContextReadBranches.None.Candidates.Should().BeEmpty();
        ContextReadBranches.ForBranch(new BranchName("x")).Candidates.Should().Equal(new BranchName("x"));
    }
}
