using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0507: the readers that DEGRADE SILENTLY on an id shape they do not know. Neither of
/// these throws on an unrecognised id — one files the spec's decisions under the run
/// instead, the other reclassifies a dependency as prose — so only a test says which
/// shapes they actually read.
/// </summary>
public sealed class SpecIdRuntimeReaderTests
{
    private const string Minted = "2026-08-24-8a3f";

    /// <summary>
    /// An unrecognised id would send a spec's decisions to the step's spec instead (2026-11-28-db88d), where the
    /// next agent reading the spec will never find them.
    /// </summary>
    [Fact]
    public void DecisionFileLabel_DateMintedSpec_WritesToItsOwnFile()
    {
        var label = DecisionFileLabel.Resolve(Minted, specId: "2026-08-24-0000");

        label.Should().NotBeNull();
        label!.FileName.Should().Be($"{Minted}.yaml");
        label.HeaderKey.Should().Be("spec", "it is a spec's decision file, not a run's");
    }

    /// <summary>2026-10-06-03c7b: a spec's decision file opens with the key the schema requires.</summary>
    [Fact]
    public void DecisionFileLabel_SpecLabel_HeaderIsSpec() =>
        DecisionFileLabel.Resolve(Minted, specId: null)!.Header
            .Should().StartWith($"spec: {Minted}\n");

    [Fact]
    public void Resolve_NoSpecLabel_UsesTheStepsSpec()
    {
        var label = DecisionFileLabel.Resolve(sourceLabel: null, specId: $"{Minted}a")!;

        label.FileName.Should().Be($"{Minted}a.yaml");
        label.Header.Should().StartWith($"spec: {Minted}a\n");
    }

    [Fact]
    public void Resolve_NoSpec_NamesNoFile()
    {
        DecisionFileLabel.Resolve(sourceLabel: null, specId: null).Should().BeNull();
        DecisionFileLabel.Resolve("spec-set", specId: null).Should().BeNull("2026-11-28-db88d: no file is named after a run or a label");
        DecisionFileLabel.Resolve("2026-08-24T09-15-00-1c2d", specId: null).Should().BeNull("a run id is not a spec");
    }

    [Fact]
    public void DecisionFileLabel_CounterSpec_StillWritesToItsOwnFile() =>
        DecisionFileLabel.Resolve("p0507", specId: null)!.FileName.Should().Be("p0507.yaml");

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

    /// <summary>
    /// 2026-09-03-2f81: the readers slice a date-minted id by FIXED WIDTH, so without the
    /// widening every member of a series reads back as the same id — two specs file their
    /// decisions into one document and a requires-edge between them becomes a self-edge.
    /// </summary>
    [Fact]
    public void DecisionFileLabel_TwoMembersOfOneSeries_WriteToTwoFiles()
    {
        var first = DecisionFileLabel.Resolve($"{Minted}a", specId: null)!.FileName;
        var second = DecisionFileLabel.Resolve($"{Minted}b", specId: null)!.FileName;

        first.Should().Be($"{Minted}a.yaml");
        second.Should().Be($"{Minted}b.yaml");
        first.Should().NotBe(second);
    }

    [Fact]
    public void RequiresEdgeChecker_ASeriesSiblingThatExists_Passes()
    {
        var first = Draft($"{Minted}a");
        var second = Draft($"{Minted}b", requires: $"{Minted}a");

        new RequiresEdgeChecker().Check(Draft("2026-08-24-0000"), [first, second])
            .Should().BeNull(
                "a series member naming the one before it is the ordinary shape of a series");
    }

    [Fact]
    public void RequiresEdgeChecker_ASeriesSiblingThatDoesNot_IsCaught()
    {
        var first = Draft($"{Minted}a");
        var second = Draft($"{Minted}b", requires: $"{Minted}c");

        new RequiresEdgeChecker().Check(Draft("2026-08-24-0000"), [first, second])
            .Should().Contain("not a sibling in this epic");
    }

    [Fact]
    public void RequiresEdgeChecker_DateMintedSiblingThatExists_Passes()
    {
        var first = Draft("2026-08-24-b17c");
        var second = Draft("2026-08-24-4d90", requires: "2026-08-24-b17c");

        new RequiresEdgeChecker().Check(Draft("2026-08-24-0000"), [first, second])
            .Should().BeNull();
    }

    private static SpecDraft Draft(string specId, string? requires = null) =>
        new(specId, "anything", Yaml: string.Empty, requires is null ? [] : [requires]);
}
