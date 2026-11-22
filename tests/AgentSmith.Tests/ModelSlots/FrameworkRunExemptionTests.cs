using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>2026-11-22-9719a: the series' own beats take no model place; 2026-11-22-6d85e: nor do conversation turns;
/// every other pipeline does, and a release forgets the exemption with the run.</summary>
public sealed class FrameworkRunExemptionTests
{
    [Theory]
    [InlineData("series-run", false)]
    [InlineData("SERIES-VERIFY", false)]
    [InlineData("fix-bug", true)]
    [InlineData("spec-dialog", false)]
    [InlineData("code", true)]
    [InlineData("file", true)]
    [InlineData("admin-turn", true)]
    public void CountsAgainstModelLimit_OnlyTheSeriesBeatsAndConversationTurnsAreExempt(string pipeline, bool counts) =>
        PipelinePresets.CountsAgainstModelLimit(pipeline).Should().Be(counts);

    [Fact]
    public async Task Enter_ASeriesBeatIsExempt_UntilItsReleaseForgetsIt()
    {
        var signal = new ModelRunSlotSignal();
        var places = new ModelPlaceRelease(NullLogger<ModelPlaceRelease>.Instance, null, signal);

        places.Enter("beat", PipelinePresets.SeriesRunName);
        places.Enter("code", "fix-bug");

        signal.IsExempt("beat").Should().BeTrue();
        signal.IsExempt("code").Should().BeFalse();
        await places.ReleaseAsync("beat");
        signal.IsExempt("beat").Should().BeFalse("the run's release forgets its exemption");
    }

    [Fact]
    public void Enter_ATurnIsExempt_ARunItStartsIsNot()
    {
        var signal = new ModelRunSlotSignal();
        var places = new ModelPlaceRelease(NullLogger<ModelPlaceRelease>.Instance, null, signal);

        places.Enter("turn", PipelinePresets.SpecDialogName);
        places.Enter("started", PipelinePresets.CodeName);

        signal.IsExempt("turn").Should().BeTrue();
        signal.IsExempt("started").Should().BeFalse("the exemption is the turn's run id, not inherited by what it starts");
    }
}
