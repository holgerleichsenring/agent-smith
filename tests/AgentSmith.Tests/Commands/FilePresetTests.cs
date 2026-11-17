using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-17-5701b: the 'file' preset — plan, then file; launched by the server only, with no master.</summary>
public sealed class FilePresetTests
{
    [Fact]
    public void File_IsPlanThenFile()
    {
        PipelinePresets.TryResolve(PipelinePresets.FileName).Should().Equal(CommandNames.PlanFiling, CommandNames.FileTickets);
        PipelinePresets.Names.Should().Contain(PipelinePresets.FileName);
    }

    [Fact]
    public void File_NeedsHostContextAndIsNotRoutable()
    {
        PipelinePresets.NeedsHostContext(PipelinePresets.FileName).Should().BeTrue();
        PipelinePresets.Routable.Should().NotContain(PipelinePresets.FileName);
        PipelinePresets.NotRoutableBecause(PipelinePresets.FileName).Should().Contain("approved proposal");
    }

    [Fact]
    public void File_RunsAsAPipelineWithNoMaster()
    {
        PipelinePresets.RunKindOf(PipelinePresets.FileName).Should().Be(RegisteredRunKind.Pipeline);
        PipelinePresets.MasterFor(PipelinePresets.FileName).Should().BeEmpty();
        PipelinePresets.ExpectsCodeChanges(PipelinePresets.FileName).Should().BeFalse();
    }
}
