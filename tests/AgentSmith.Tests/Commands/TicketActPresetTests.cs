using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-17-e061a: the presets of the acts on a filed ticket — one step each, launched by the server only,
/// no master.</summary>
public sealed class TicketActPresetTests
{
    public static TheoryData<string, string> Acts => new()
    {
        { PipelinePresets.CloseName, CommandNames.CloseTicket },
        { PipelinePresets.ReopenName, CommandNames.ReopenTicket },
        { PipelinePresets.StartName, CommandNames.StartTicket },
        { PipelinePresets.AmendName, CommandNames.AmendTicket },
        { PipelinePresets.CommentName, CommandNames.PostTicketComments },
        { PipelinePresets.TicketBodyName, CommandNames.RenderTicketBody }, // 2026-11-17-dd05b
        { PipelinePresets.RetargetName, CommandNames.RetargetPullRequests }, // 2026-11-17-9a39c
    };

    [Theory]
    [MemberData(nameof(Acts))]
    public void TicketAct_IsOneStep_HostOnly_WithNoMaster(string preset, string step)
    {
        PipelinePresets.TryResolve(preset).Should().Equal(step);
        PipelinePresets.NeedsHostContext(preset).Should().BeTrue();
        PipelinePresets.Routable.Should().NotContain(preset);
        PipelinePresets.NotRoutableBecause(preset).Should().Contain("filed ticket");
        PipelinePresets.RunKindOf(preset).Should().Be(RegisteredRunKind.Pipeline);
        PipelinePresets.MasterFor(preset).Should().BeEmpty();
        PipelinePresets.ExpectsCodeChanges(preset).Should().BeFalse();
    }
}
