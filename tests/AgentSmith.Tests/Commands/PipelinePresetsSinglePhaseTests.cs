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
    public void IsSinglePhase_DiscussionPresetsWithoutReviewOrFinal_ReturnsTrue(string name) =>
        PipelinePresets.IsSinglePhase(name).Should().BeTrue();

    [Theory]
    [InlineData("fix-bug")]
    [InlineData("add-feature")]
    [InlineData("security-scan")]
    [InlineData("api-security-scan")]
    public void IsSinglePhase_PresetsWithReviewAndFinal_ReturnsFalse(string name) =>
        PipelinePresets.IsSinglePhase(name).Should().BeFalse();

    [Fact]
    public void IsSinglePhase_FixNoTest_ContainsReviewAndFinal_ReturnsFalse() =>
        // FixNoTest is Hierarchical and includes RunReviewPhase + RunFinalPhase.
        PipelinePresets.IsSinglePhase("fix-no-test").Should().BeFalse();

    [Fact]
    public void IsSinglePhase_UnknownPreset_DefaultsToTrue() =>
        PipelinePresets.IsSinglePhase("nonexistent-preset").Should().BeTrue();
}
