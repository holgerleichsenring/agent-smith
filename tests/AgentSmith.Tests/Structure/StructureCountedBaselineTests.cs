using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Models.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureGateFixture;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-11-24-8756: a baseline written before units counted the breaks a formatter would add holds
/// physical lines; under it the gate tolerates a unit no larger than at the base, so untouched code
/// never fails because the count changed. One that says '# counted: breaks' tolerates by its entries.
/// </summary>
public sealed class StructureCountedBaselineTests
{
    // A.Long() is 5 physical lines; 'a(); b();' on one line adds a break, so it counts 6 (limit 4).
    private const string Joined = "class A\n{\n    void Long()\n    {\n        a(); b();\n        c();\n    }\n}\n";
    private const string Smaller = "class A\n{\n    void Long()\n    {\n        a(); b();\n    }\n}\n";
    // 4 physical lines, within the limit of 4; three statements on one line count 6.
    private const string Within = "class A\n{\n    void Long()\n    {\n        a(); b(); c();\n    }\n}\n";
    private const string Counted = "# counted: breaks\n";

    private static async Task<StructureGateVerdict> JudgeAsync(string baseline, string? atBase, string now = Joined)
    {
        var repo = Repo(baseline);
        repo.Files["src/Api/A.cs"] = now;
        if (atBase is not null) repo.BaseFiles["src/Api/A.cs"] = atBase;
        repo.Changed.Add("src/Api/A.cs");
        return await Gate().JudgeAsync(PipelineOf(repo, ("api", "src/Api")), CancellationToken.None);
    }

    [Fact]
    public async Task Tolerates_OldBaseline_UnitUnchangedSinceTheBase()
    {
        var listed = await JudgeAsync("src/Api/A.cs\tA.Long()\tfunction_lines\t5\n", Joined);
        var unlisted = await JudgeAsync(string.Empty, Within, Within);

        listed.Passed.Should().BeTrue("the entry was measured in physical lines, and the unit is as it was");
        unlisted.Passed.Should().BeTrue("an untouched unit within its limit never fails because the count changed");
    }

    [Fact]
    public async Task Tolerates_OldBaseline_UnitPastTheBandAtTheBase_StaysAnExcess()
    {
        var verdict = await JudgeAsync(string.Empty, Joined);

        verdict.Excess.Should().ContainSingle("the old rule already failed it at the base; the count did not cause it");
    }

    [Fact]
    public async Task Tolerates_OldBaselineAndGrownUnit_IsAnExcess()
    {
        var verdict = await JudgeAsync("src/Api/A.cs\tA.Long()\tfunction_lines\t5\n", Smaller);

        verdict.Excess.Should().ContainSingle().Which.Value.Should().Be(6, "the unit grew since the base");
    }

    [Fact]
    public async Task Tolerates_CountedBaseline_UsesTheCount()
    {
        var verdict = await JudgeAsync(Counted + "src/Api/A.cs\tA.Long()\tfunction_lines\t5\n", Joined);

        verdict.Excess.Should().ContainSingle().Which.Value.Should().Be(6, "a counted entry is compared with the count");
    }

    [Fact]
    public async Task BaseCounts_FileAtTheBase_CountsItsUnits()
    {
        var repo = new RepoSandbox();
        repo.BaseFiles["src/A.cs"] = Joined;
        var counts = new StructureBaseCounts(StructureEngineFixture.Engine(), StructureBaselineStubs.GitReads());

        var read = await counts.ReadAsync(repo, RepoSandbox.MergeBase, "src/A.cs", CancellationToken.None);
        var absent = await counts.ReadAsync(repo, RepoSandbox.MergeBase, "src/B.cs", CancellationToken.None);

        read[("A.Long()", StructureMetric.FunctionLines)].Should().Match<StructureUnit>(u => u.Lines == 6 && u.PhysicalLines == 5);
        absent.Should().BeEmpty();
    }

    [Fact]
    public void Refresh_OldBaseline_TranslatesEntriesIntoCounts()
    {
        var file = StructureBaselineStubs.File;
        var old = file.Parse("p\tk\tfunction_lines\t70\np\tgrown\tfunction_lines\t70\n");
        var measured = new[]
        {
            new StructureFinding("p", "k", "function_lines", 74, 55, PhysicalValue: 70),
            new StructureFinding("p", "grown", "function_lines", 84, 55, PhysicalValue: 80),
            new StructureFinding("p", "new", "function_lines", 70, 55, PhysicalValue: 60),
        };
        var limits = new StructureLimitSet(new Dictionary<string, int> { ["function_lines"] = 55 }, 25);

        var refreshed = file.Refresh(old, measured, limits, oldCountsBreaks: false);

        refreshed.Should().Contain(e => e.Key == "k" && e.Value == 74, "a physical entry becomes the unit's count");
        refreshed.Should().Contain(e => e.Key == "grown" && e.Value == 74, "growth since the entry stays outside it");
        refreshed.Should().Contain(e => e.Key == "new" && e.Value == 70,
            "a unit within the old band in physical lines is what the new count exposes");
        file.Refresh(old, measured, limits).Should().Contain(e => e.Key == "k" && e.Value == 70,
            "a counted baseline keeps the lower value");
    }

    [Fact]
    public void Render_WritesTheCountedMarker()
    {
        var file = StructureBaselineStubs.File;

        var rendered = file.Render([new StructureBaselineEntry("p", "k", "function_lines", 70)], null);

        file.CountsBreaks(rendered).Should().BeTrue();
        file.Parse(rendered).Should().ContainSingle();
        file.CountsBreaks(file.Render([], null, countsBreaks: false)).Should().BeFalse();
        file.CountsBreaks("# path\tkey\tmetric\tvalue\np\tk\tfunction_lines\t70\n").Should().BeFalse();
    }
}
