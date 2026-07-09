using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

public sealed class PipelineDataFlowsCoverageTests
{
    [Fact]
    public void EveryPipelinePreset_HasMatchingPhaseDataFlow()
    {
        var declared = new IPhaseDataFlow[]
        {
            new FixBugDataFlow(), new FixNoTestDataFlow(), new AddFeatureDataFlow(),
            new InitProjectDataFlow(), new SecurityScanDataFlow(),
            new ApiSecurityScanDataFlow(), new MadDiscussionDataFlow(),
            new LegalAnalysisDataFlow(), new SkillManagerDataFlow(), new AutonomousDataFlow(),
            new PrReviewDataFlow()
        };
        var declaredNames = declared
            .Select(d => d.PresetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var presetNames = PipelinePresets.Names.ToHashSet(StringComparer.OrdinalIgnoreCase);

        declaredNames.Should().BeEquivalentTo(presetNames,
            "every pipeline preset must have a matching IPhaseDataFlow declaration");
    }

    [Fact]
    public void EveryPhaseDataFlow_HasNonEmptyEdges()
    {
        var declared = new IPhaseDataFlow[]
        {
            new FixBugDataFlow(), new FixNoTestDataFlow(), new AddFeatureDataFlow(),
            new InitProjectDataFlow(), new SecurityScanDataFlow(),
            new ApiSecurityScanDataFlow(), new MadDiscussionDataFlow(),
            new LegalAnalysisDataFlow(), new SkillManagerDataFlow(), new AutonomousDataFlow(),
            new PrReviewDataFlow()
        };

        foreach (var flow in declared)
            flow.Edges.Should().NotBeEmpty(
                $"{flow.PresetName} declared no edges; the gate would block every read");
    }
}
