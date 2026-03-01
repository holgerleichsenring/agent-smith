using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public sealed class PipelineContextTests
{
    [Fact]
    public void TrackCommand_CreatesTrailEntry()
    {
        var pipeline = new PipelineContext();
        var duration = TimeSpan.FromMilliseconds(150);

        pipeline.TrackCommand("FetchTicketCommand", true, "done", duration, null);

        var trail = pipeline.Get<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail);
        trail.Should().HaveCount(1);
        trail[0].CommandName.Should().Be("FetchTicketCommand");
        trail[0].Success.Should().BeTrue();
        trail[0].Message.Should().Be("done");
        trail[0].Duration.Should().Be(duration);
        trail[0].InsertedCommandCount.Should().BeNull();
    }

    [Fact]
    public void TrackCommand_MultipleEntries_AppendToTrail()
    {
        var pipeline = new PipelineContext();

        pipeline.TrackCommand("Step1", true, "ok", TimeSpan.FromMilliseconds(10), null);
        pipeline.TrackCommand("Step2", false, "failed", TimeSpan.FromMilliseconds(20), null);

        var trail = pipeline.Get<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail);
        trail.Should().HaveCount(2);
        trail[0].CommandName.Should().Be("Step1");
        trail[1].CommandName.Should().Be("Step2");
        trail[1].Success.Should().BeFalse();
    }

    [Fact]
    public void TrackCommand_WithInsertedCount_RecordsIt()
    {
        var pipeline = new PipelineContext();

        pipeline.TrackCommand("Triage", true, "done", TimeSpan.Zero, 3);

        var trail = pipeline.Get<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail);
        trail[0].InsertedCommandCount.Should().Be(3);
    }

    [Fact]
    public void TrackCommand_IncludesActiveSkill()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ActiveSkill, "architect");

        pipeline.TrackCommand("SkillRound", true, "ok", TimeSpan.Zero, null);

        var trail = pipeline.Get<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail);
        trail[0].Skill.Should().Be("architect");
    }

    [Fact]
    public void TrackCommand_NoActiveSkill_SkillIsNull()
    {
        var pipeline = new PipelineContext();

        pipeline.TrackCommand("FetchTicket", true, "ok", TimeSpan.Zero, null);

        var trail = pipeline.Get<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail);
        trail[0].Skill.Should().BeNull();
    }
}
