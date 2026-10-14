using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Turns;

/// <summary>
/// A sub-agent's steps reach the turn's observer under the sub-agent's name, and the frame
/// unwinds to the enclosing observer when it closes.
/// </summary>
public sealed class SubAgentActivityFrameTests
{
    [Fact]
    public async Task Open_WhileObserved_ReportsStepsUnderTheSubAgentsName()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);

        using (new SubAgentActivityFrame(accessor).Open("RepoScout"))
            await accessor.ReportAsync(new TurnActivity(TurnActivityKind.Tool, "grep", "Router"), CancellationToken.None);
        await accessor.ReportAsync(new TurnActivity(TurnActivityKind.Tool, "read_file"), CancellationToken.None);

        recorder.Lines.Should().Equal("tool RepoScout › grep Router", "tool read_file");
    }

    [Fact]
    public async Task Open_WithNobodyObserving_OpensNothing()
    {
        var accessor = TurnActivityRecorder.Silent();

        using (new SubAgentActivityFrame(accessor).Open("RepoScout"))
            accessor.Current.Should().BeNull();

        await accessor.ReportAsync(new TurnActivity(TurnActivityKind.Model), CancellationToken.None);
    }

    [Fact]
    public async Task ReportAsync_AStepWithoutAName_IsNamedByTheSubAgentAlone()
    {
        var recorder = new TurnActivityRecorder();

        await new NamedTurnActivityObserver(recorder, "RepoScout")
            .ReportAsync(new TurnActivity(TurnActivityKind.Revising), CancellationToken.None);

        recorder.Seen.Should().ContainSingle().Which.Name.Should().Be("RepoScout");
    }
}
