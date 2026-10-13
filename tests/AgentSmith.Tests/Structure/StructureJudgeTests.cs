using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class StructureJudgeTests
{
    private static readonly StructureMeasurement Measured = new(
        StructureOutcome.Measured,
        [
            new StructureUnit(StructureUnitKind.Type, "A", 1, 200),
            new StructureUnit(StructureUnitKind.Function, "A.Long()", 2, 70),
            new StructureUnit(StructureUnitKind.Function, "A.Short()", 71, 75),
        ],
        FileLines: 200, TypeCount: 2);

    [Fact]
    public void Judge_FunctionOverLimit_ReturnsOneFinding()
    {
        var limits = new StructureLimitSet(new Dictionary<string, int> { [StructureMetric.FunctionLines] = 55 });

        var findings = new StructureJudge().Judge("src/A.cs", Measured, limits);

        findings.Should().ContainSingle()
            .Which.Should().Be(new StructureFinding("src/A.cs", "A.Long()", StructureMetric.FunctionLines, 69, 55));
    }

    [Fact]
    public void Judge_FileMetrics_UseTheFileKey()
    {
        var limits = new StructureLimitSet(new Dictionary<string, int>
        {
            [StructureMetric.FileLines] = 150, [StructureMetric.TypesPerFile] = 1,
        });

        var findings = new StructureJudge().Judge("src/A.cs", Measured, limits);

        findings.Select(f => (f.Key, f.Metric)).Should().BeEquivalentTo(new[]
        {
            (StructureMetric.FileKey, StructureMetric.FileLines),
            (StructureMetric.FileKey, StructureMetric.TypesPerFile),
        });
    }

    [Fact]
    public void Judge_AbsentKey_JudgesNothingForThatMetric()
    {
        new StructureJudge().Judge("src/A.cs", Measured, StructureLimitSet.None).Should().BeEmpty();
    }

    [Fact]
    public void Judge_NotMeasured_ReturnsNothing()
    {
        var limits = new StructureLimitSet(new Dictionary<string, int> { [StructureMetric.FileLines] = 1 });

        new StructureJudge().Judge("a.md", StructureMeasurement.NotMeasured("x"), limits).Should().BeEmpty();
    }
}
