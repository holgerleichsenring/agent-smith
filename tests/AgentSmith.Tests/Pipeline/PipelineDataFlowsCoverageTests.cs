using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

public sealed class PipelineDataFlowsCoverageTests
{
    [Fact]
    public void EveryPipelinePreset_HasMatchingStepDataFlow()
    {
        var declared = new IStepDataFlow[]
        {
            new CodeDataFlow(), new CodeDataFlow(), new CodeDataFlow(),
            new InitProjectDataFlow(), new SecurityScanDataFlow(),
            new ApiSecurityScanDataFlow(), new MadDiscussionDataFlow(),
            new LegalAnalysisDataFlow(),
            new PrReviewDataFlow(),
            new SpecDialogDataFlow(), new CodeDataFlow()
        };
        var declaredNames = declared
            .Select(d => d.PresetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var presetNames = PipelinePresets.Names.ToHashSet(StringComparer.OrdinalIgnoreCase);

        declaredNames.Should().BeEquivalentTo(presetNames,
            "every pipeline preset must have a matching IStepDataFlow declaration");
    }

    [Fact]
    public void EveryStepDataFlow_HasNonEmptyEdges()
    {
        var declared = new IStepDataFlow[]
        {
            new CodeDataFlow(), new CodeDataFlow(), new CodeDataFlow(),
            new InitProjectDataFlow(), new SecurityScanDataFlow(),
            new ApiSecurityScanDataFlow(), new MadDiscussionDataFlow(),
            new LegalAnalysisDataFlow(),
            new PrReviewDataFlow(),
            new SpecDialogDataFlow(), new CodeDataFlow()
        };

        foreach (var flow in declared)
            flow.Edges.Should().NotBeEmpty(
                $"{flow.PresetName} declared no edges; the gate would block every read");
    }
}
