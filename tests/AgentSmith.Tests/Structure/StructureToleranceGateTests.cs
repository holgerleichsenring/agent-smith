using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-12-fbbda: the gate, the PR-time judgment and the baseline act on excess only.</summary>
public sealed class StructureToleranceGateTests
{
    // A.Long() measures 5 lines; under function_lines 4 and tolerance 50 the band ends at 6.
    private const string Long = "class A\n{\n    void Long()\n    {\n        a();\n        b();\n    }\n}\n";
    private const string Banded = "## Limits\n\n```yaml\nfunction_lines: 4\ntolerance_percent: 50\n```\n";

    private static RepoSandbox BandedRepo()
    {
        var repo = Repo();
        repo.BaseFiles[".agentsmith/contexts/api/principles.md"] = Banded;
        repo.Files["src/Api/A.cs"] = Long;
        repo.Changed.Add("src/Api/A.cs");
        return repo;
    }

    [Fact]
    public async Task StructureGate_OnlyDivergences_Passes()
    {
        var verdict = await Gate().JudgeAsync(PipelineOf(BandedRepo(), ("api", "src/Api")), CancellationToken.None);

        verdict.Passed.Should().BeTrue();
        verdict.Divergences.Should().ContainSingle().Which.Repository.Should().Be(Key);
    }

    [Fact]
    public async Task StructureGate_Excess_CarriesItsRepository()
    {
        var repo = BandedRepo();
        repo.BaseFiles[".agentsmith/contexts/api/principles.md"] = Limits;

        var verdict = await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);

        verdict.Excess.Should().ContainSingle().Which.Repository.Should().Be(Key);
    }

    [Fact]
    public async Task StructureGate_DivergenceInBaseline_IsStillNamed()
    {
        var repo = BandedRepo();
        repo.BaseFiles[".agentsmith/contexts/api/structure-baseline.tsv"] = "src/Api/A.cs\tA.Long()\tfunction_lines\t9\n";

        var verdict = await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);

        verdict.Divergences.Should().ContainSingle("a unit already inside the band at the base is listed too");
    }

    [Fact]
    public async Task DivergencePublisher_PutsEveryDivergenceOnThePipeline()
    {
        var pipeline = PipelineOf(BandedRepo(), ("api", "src/Api"));

        await Publisher().PublishAsync(pipeline, CancellationToken.None);

        pipeline.Get<IReadOnlyList<StructureFinding>>(ContextKeys.StructureDivergences)
            .Should().ContainSingle().Which.Should().Be(new StructureFinding(
                "src/Api/A.cs", "A.Long()", StructureMetric.FunctionLines, 5, 4, StructureSeverity.Divergence, Key));
    }

    [Fact]
    public async Task StructureBaseline_Divergence_IsNotRecorded()
    {
        var repo = StructureBaselineRepo.Repo(
            StructureBaselineRepo.Class(StructureBaselineRepo.Method("Mid", 2), StructureBaselineRepo.Method("Long", 5)),
            Banded);

        var result = await StructureBaselineRepo.Apply(repo);

        result.Entries.Should().Be(1);
        StructureBaselineStubs.File.Parse(repo.Files[StructureBaselineRepo.Baseline]).Should().ContainSingle()
            .Which.Key.Should().Be("A.Long()", "A.Mid() measures 5, inside the band that ends at 6");
    }

    [Fact]
    public void StructureBaselineRefresh_UnitInsideTheOldBand_IsExposed()
    {
        var oldLimits = new StructureLimitSet(new Dictionary<string, int> { [StructureMetric.FunctionLines] = 4 }, 50);
        var measured = new StructureFinding("src/A.cs", "A.Long()", StructureMetric.FunctionLines, 6, 3);

        StructureBaselineStubs.File.Refresh([], [measured], oldLimits).Should().ContainSingle(
            "6 sat inside the old band, so the lowered limit is what exposed it");
    }
}
