using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0131c-pre: <see cref="PipelinePresets.IsSinglePhase"/> drives
/// <c>StructuredTriageStrategy</c>'s phase-collapse logic.
/// </summary>
public sealed class PipelinePresetsSinglePhaseTests
{
    [Theory]
    [InlineData("mad-discussion")]
    [InlineData("legal-analysis")]
    [InlineData("autonomous")]
    [InlineData("init-project")]
    [InlineData("skill-manager")]
    [InlineData("fix-bug")]
    [InlineData("add-feature")]
    [InlineData("fix-no-test")]
    [InlineData("security-scan")]
    [InlineData("api-security-scan")]
    public void IsSinglePhase_PresetsAfterCollapse_ReturnsTrue(string name) =>
        // p0179b: coding presets shed Review/Final.
        // p0179d: scan + legal-analysis presets shed them too.
        // mad-discussion still has no Review/Final (it goes through
        // ConvergenceCheck instead) so it's single-phase too.
        PipelinePresets.IsSinglePhase(name).Should().BeTrue();

    [Fact]
    public void IsSinglePhase_UnknownPreset_DefaultsToTrue() =>
        PipelinePresets.IsSinglePhase("nonexistent-preset").Should().BeTrue();
}
