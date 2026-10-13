using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-12-fbbda: the tolerance band — how it is read and how a unit is classified.</summary>
public sealed class StructureToleranceTests
{
    // A.Long() spans lines 2..70: 69 lines.
    private static readonly StructureMeasurement Measured = new(
        StructureOutcome.Measured, [new StructureUnit(StructureUnitKind.Function, "A.Long()", 2, 70)],
        FileLines: 80, TypeCount: 1);

    private static StructureLimitSet FunctionLimit(int limit, int tolerance) =>
        new(new Dictionary<string, int> { [StructureMetric.FunctionLines] = limit }, tolerance);

    private static string Fence(string heading, string body) => $"{heading}\n\n```yaml\n{body}\n```\n";

    [Fact]
    public void StructureJudge_ValueInsideBand_IsDivergence()
    {
        // floor(55 * 130 / 100) = 71 >= 69
        new StructureJudge().Judge("src/A.cs", Measured, FunctionLimit(55, 30))
            .Should().ContainSingle().Which.Severity.Should().Be(StructureSeverity.Divergence);
    }

    [Fact]
    public void StructureJudge_ValuePastBand_IsExcess()
    {
        // floor(55 * 125 / 100) = 68 < 69
        new StructureJudge().Judge("src/A.cs", Measured, FunctionLimit(55, 25))
            .Should().ContainSingle().Which.Severity.Should().Be(StructureSeverity.Excess);
    }

    [Fact]
    public void StructureJudge_NoTolerance_IsStrict()
    {
        new StructureJudge().Judge("src/A.cs", Measured, FunctionLimit(68, 0))
            .Should().ContainSingle().Which.IsExcess.Should().BeTrue();
    }

    [Fact]
    public void StructureLimitSet_BandEnd_IsFloored()
    {
        FunctionLimit(20, 25).BandEnd(20).Should().Be(25);
        FunctionLimit(55, 25).BandEnd(55).Should().Be(68);
    }

    [Fact]
    public void StructureLimits_DeltaTolerance_IsRead()
    {
        var limits = new StructureLimits().Read(Fence("## Limits", "function_lines: 20\ntolerance_percent: 25"));

        limits.Tolerance.Should().Be(25);
        limits.For(StructureMetric.FunctionLines).Should().Be(20);
    }

    [Fact]
    public void StructureLimits_HouseToleranceZero_OverridesDelta()
    {
        var principles = Fence("## Limits", "function_lines: 20\ntolerance_percent: 25")
            + "\n## Project Specifics\n\n" + Fence("### Limits", "tolerance_percent: 0");

        var limits = new StructureLimits().Read(principles);

        limits.Tolerance.Should().Be(0, "a house fence overrides the delta and 0 makes the context strict");
        limits.For(StructureMetric.FunctionLines).Should().Be(20);
    }

    [Fact]
    public void StructureLimits_ToleranceOnly_IsEmpty()
    {
        var limits = new StructureLimits().Read(Fence("## Limits", "tolerance_percent: 25"));

        limits.IsEmpty.Should().BeTrue("a tolerance is not a limit");
        limits.Tolerance.Should().Be(25);
    }

    [Fact]
    public void StructureLimits_NegativeTolerance_IsIgnored()
    {
        new StructureLimits().Read(Fence("## Limits", "function_lines: 20\ntolerance_percent: -5"))
            .Tolerance.Should().Be(0);
    }
}
