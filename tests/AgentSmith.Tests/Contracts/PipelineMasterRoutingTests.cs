using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>
/// p0408: the pipeline→master table moved next to the presets so the run and the generated
/// diagram resolve the same master. These pin the routing itself.
/// </summary>
public sealed class PipelineMasterRoutingTests
{
    [Fact]
    public void MasterFor_EveryPreset_NamesAMaster()
    {
        foreach (var preset in PipelinePresets.Names)
            PipelinePresets.MasterFor(preset).Should().NotBeNullOrWhiteSpace(
                $"preset '{preset}' runs an AgenticMaster step and must name the master it loads");
    }

    [Theory]
    [InlineData("security-scan", "security-master")]
    [InlineData("api-security-scan", "api-security-master")]
    [InlineData("legal-analysis", "legal-analyst-master")]
    [InlineData("mad-discussion", "mad-discussion-master")]
    [InlineData("pr-review", "pr-review-master")]
    [InlineData("spec-dialog", "design-partner-master")]
    public void MasterFor_MappedPipeline_ResolvesItsOwnMaster(string pipeline, string master) =>
        PipelinePresets.MasterFor(pipeline).Should().Be(master);

    [Fact]
    public void MasterFor_Code_ResolvesTheCodingMaster()
    {
        // 2026-09-25-e5b1: the four retired names used to be asserted here too, through the
        // alias map. They resolve to nothing now, which the fallback below covers — and what
        // a configuration still naming one should say is the startup finding's job, not this
        // table's.
        PipelinePresets.MasterFor(PipelinePresets.CodeName).Should().Be(PipelinePresets.CodingMaster);
    }

    [Fact]
    public void MasterFor_UnknownPipeline_FallsBackToTheCodingMaster() =>
        PipelinePresets.MasterFor("not-a-preset").Should().Be(PipelinePresets.CodingMaster);
}
