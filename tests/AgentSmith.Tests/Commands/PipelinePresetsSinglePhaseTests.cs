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
    public void IsSinglePhase_PresetsWithoutReviewOrFinal_ReturnsTrue(string name) =>
        // p0179b: coding presets (fix-bug / add-feature / fix-no-test) shed
        // RunReviewPhase + RunFinalPhase when their choreography moved into
        // coding-agent-master; they are single-phase from the orchestrator's
        // point of view now too.
        PipelinePresets.IsSinglePhase(name).Should().BeTrue();

    [Theory]
    [InlineData("security-scan")]
    [InlineData("api-security-scan")]
    public void IsSinglePhase_ScanPresetsWithReviewAndFinal_ReturnsFalse(string name) =>
        // Scan pipelines still run the old choreography until p0179d migrates
        // them onto a security-master / api-security-master skill.
        PipelinePresets.IsSinglePhase(name).Should().BeFalse();

    [Fact]
    public void IsSinglePhase_UnknownPreset_DefaultsToTrue() =>
        PipelinePresets.IsSinglePhase("nonexistent-preset").Should().BeTrue();
}
