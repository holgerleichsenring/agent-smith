using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0507: the readers that DEGRADE SILENTLY on an id shape they do not know. Neither of
/// these throws on an unrecognised id — one files the phase's decisions under the run
/// instead, the other reclassifies a dependency as prose — so only a test says which
/// shapes they actually read.
/// </summary>
public sealed class PhaseIdRuntimeReaderTests
{
    private const string Minted = "2026-08-24-8a3f";

    /// <summary>
    /// An unrecognised id sends a phase's decisions to decisions/{runId}.yaml, where the
    /// next agent reading the phase will never find them.
    /// </summary>
    [Fact]
    public void DecisionFileLabel_DateMintedPhase_WritesToItsOwnFile()
    {
        var label = DecisionFileLabel.Resolve(Minted, runId: "2026-08-24T09-15-00-1c2d");

        label.Should().NotBeNull();
        label!.FileName.Should().Be($"{Minted}.yaml");
        label.HeaderKey.Should().Be("phase", "it is a phase's decision file, not a run's");
    }

    [Fact]
    public void DecisionFileLabel_CounterPhase_StillWritesToItsOwnFile() =>
        DecisionFileLabel.Resolve("p0507", runId: null)!.FileName.Should().Be("p0507.yaml");

    /// <summary>
    /// A requirement the checker does not recognise is treated as a free-text precondition
    /// and skipped, so sibling, parent and cycle checks pass in silence for a whole epic.
    /// </summary>
    [Fact]
    public void RequiresEdgeChecker_DateMintedSibling_IsCheckedNotTreatedAsProse()
    {
        var parent = Draft("2026-08-24-0000");
        var child = Draft("2026-08-24-b17c", requires: $"{Minted}-not-a-sibling");

        var error = new RequiresEdgeChecker().Check(parent, [child]);

        error.Should().NotBeNull("an id-shaped requirement naming no sibling must be caught");
        error.Should().Contain("not a sibling in this epic");
    }

    [Fact]
    public void RequiresEdgeChecker_DateMintedSiblingThatExists_Passes()
    {
        var first = Draft("2026-08-24-b17c");
        var second = Draft("2026-08-24-4d90", requires: "2026-08-24-b17c");

        new RequiresEdgeChecker().Check(Draft("2026-08-24-0000"), [first, second])
            .Should().BeNull();
    }

    private static PhaseDraft Draft(string phaseId, string? requires = null) =>
        new(phaseId, "anything", Yaml: string.Empty, requires is null ? [] : [requires]);
}
