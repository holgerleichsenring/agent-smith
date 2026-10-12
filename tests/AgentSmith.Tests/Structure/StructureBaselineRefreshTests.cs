using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureBaselineRepo;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a refresh keeps what is still over, lower, and adds only what a changed limit exposes.
/// 2026-10-13-2d0c: "changed" is measured against the Limits the baseline's header records.
/// </summary>
public sealed class StructureBaselineRefreshTests
{
    private static StructureBaselineFile File => StructureBaselineStubs.File;

    private static IEnumerable<string> Keys(RepoSandbox repo) => File.Parse(repo.Files[Baseline]).Select(e => e.Key);

    [Fact]
    public async Task StructureBaseline_Refresh_DropsFixedAndLowersShrunkEntries()
    {
        var repo = Repo(Class(Method("Long", 3), Method("Gone", 1)));
        repo.Files[Baseline] = File.Render([
            new("src/Api/A.cs", "A.Long()", "function_lines", 9),
            new("src/Api/A.cs", "A.Gone()", "function_lines", 8)], LimitSet(4));

        var result = await Apply(repo, PrinciplesMode.Refreshed);

        result.Should().Be(new StructureBaselineResult(true, 1, 0, true));
        File.Parse(repo.Files[Baseline]).Should().ContainSingle()
            .Which.Value.Should().Be(6, "the entry is lowered to what is measured now");
    }

    [Fact]
    public async Task StructureBaseline_Refresh_AddsOnlyUnitsALoweredLimitExposes()
    {
        // Recorded limit 10, new limit 4: Exposed (6 lines) was green before; Grown (12) passed an old limit.
        var repo = Repo(Class(Method("Exposed", 3), Method("Grown", 9)));
        repo.Files[Baseline] = File.Render([], LimitSet(10));

        var result = await Apply(repo, PrinciplesMode.Refreshed);

        Keys(repo).Should().Equal("A.Exposed()");
        result.Sentence().Should().Contain("a unit grown past an unchanged limit surfaces at the gate");
    }

    [Fact]
    public async Task StructureBaseline_Refresh_AddsUnitsOfAMetricTheOldLimitsDidNotLimit()
    {
        var repo = Repo(Class(Method("Exposed", 3), Method("Large", 9)));
        repo.Files[Baseline] = File.Render([], new(new Dictionary<string, int> { ["type_lines"] = 100 }));

        await Apply(repo, PrinciplesMode.Refreshed);

        Keys(repo).Should().BeEquivalentTo("A.Exposed()", "A.Large()");
    }

    [Fact]
    public async Task StructureBaseline_RefreshAfterManualLimitChange_AddsTheExposedUnit()
    {
        // The operator lowered 10 to 4 in principles.md by hand: the text the round starts from already says 4.
        var repo = Repo(Class(Method("Exposed", 3)), principles: Limits(4));
        repo.Files[Baseline] = File.Render([], LimitSet(10));

        await Apply(repo, PrinciplesMode.Refreshed);

        Keys(repo).Should().Equal(["A.Exposed()"], "the baseline was measured against 10, which A.Exposed() met");
        File.MeasuredWith(repo.Files[Baseline]).Should().BeEquivalentTo(LimitSet(4), "the refresh records its own limits");
    }

    [Fact]
    public async Task StructureBaseline_RefreshAfterToleranceLowered_AddsTheExposedUnit()
    {
        // Recorded: limit 4 with 50 % (band ends at 6). Now strict: Exposed (6) is over, Grown (9) was over before.
        var repo = Repo(Class(Method("Exposed", 3), Method("Grown", 6)));
        repo.Files[Baseline] = File.Render([], LimitSet(4, tolerance: 50));

        await Apply(repo, PrinciplesMode.Refreshed);

        Keys(repo).Should().Equal("A.Exposed()");
    }

    [Fact]
    public async Task StructureBaseline_RefreshOfHeaderlessBaseline_AddsEveryUnitOverItsBand()
    {
        var repo = Repo(Class(Method("Exposed", 3), Method("Grown", 9)));
        repo.Files[Baseline] = "# path\tkey\tmetric\tvalue — units over their Limits when measured, each tolerated up to its value\n";

        await Apply(repo, PrinciplesMode.Refreshed);

        Keys(repo).Should().BeEquivalentTo(["A.Exposed()", "A.Grown()"], "the old limits are unknown");
        File.MeasuredWith(repo.Files[Baseline]).Should().BeEquivalentTo(LimitSet(4));
    }

    [Fact]
    public async Task StructureBaseline_RefreshThatStops_SaysTheExistingFileStands()
    {
        var repo = Repo(Class(Method("Long", 5)), principles: "## Additions\n");
        repo.Files[Baseline] = File.Render([], LimitSet(4));

        var sentence = (await Apply(repo, PrinciplesMode.Refreshed)).Sentence();

        sentence.Should().StartWith("structure baseline not refreshed — principles.md declares no Limits")
            .And.Contain("left as it was");
    }
}
