using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9b: the run reads its act from the thread it fetched.</summary>
public sealed class ReworkActReaderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly PreviousAttempt Attempt = new("run-1", "success", Start, true);

    [Fact]
    public void ReworkActReader_KeywordAfterAttempt_SetsAct()
    {
        var act = ReworkActReader.Read(
            [new TicketComment("alice", Start.AddMinutes(10), "@agent-smith rename it"),
             new TicketComment("bob", Start.AddMinutes(20), "@agent-smith and the docs")],
            "@agent-smith", Attempt);

        act.Should().Be(new ReworkAct("bob", Start.AddMinutes(20)));
    }

    [Fact]
    public void ReworkActReader_OurCommentOrOlder_SetsNothing()
    {
        ReworkActReader.Read(
            [new TicketComment("alice", Start.AddMinutes(-10), "@agent-smith rename it"),
             new TicketComment("agent", Start.AddMinutes(10), "Agent Smith — @agent-smith refused")],
            "@agent-smith", Attempt).Should().BeNull();
    }

    [Fact]
    public void ReworkActReader_SuccessSummaryWithKeyword_SetsNothing()
    {
        ReworkActReader.Read(
            [new TicketComment("agent", Start.AddMinutes(30), "## Agent Smith - Completed across 1 repo(s)\nask @agent-smith again")],
            "@agent-smith", Attempt).Should().BeNull();
    }

    [Fact]
    public void ReworkActReader_NoPreviousAttempt_SetsNothing()
    {
        ReworkActReader.Read([new TicketComment("alice", Start, "@agent-smith go")], "@agent-smith", null)
            .Should().BeNull();
    }

    [Fact]
    public void Apply_ThreadAndAttemptOnThePipeline_SetsTheContextKey()
    {
        // Whoever claimed the ticket, the run's own fetch carries the thread and the attempt.
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.TicketComments,
            (IReadOnlyList<TicketComment>)[new TicketComment("alice", Start.AddMinutes(10), "@agent-smith go")]);
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.PreviousAttempt, Attempt);

        ReworkActReader.Apply(pipeline, "@agent-smith");

        pipeline.TryGet<ReworkAct>(AgentSmith.Contracts.Commands.ContextKeys.ReworkAct, out var act).Should().BeTrue();
        act!.Author.Should().Be("alice");
    }
}
