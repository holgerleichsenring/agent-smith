using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureBaselineRepo;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-06-8565c: a refresh keeps what is still over, lower, and adds only what a changed limit exposes.</summary>
public sealed class StructureBaselineRefreshTests
{
    [Fact]
    public async Task StructureBaseline_Refresh_DropsFixedAndLowersShrunkEntries()
    {
        var repo = Repo(Class(Method("Long", 3), Method("Gone", 1)));
        repo.Files[Baseline] = StructureBaselineFile.Render([
            new("src/Api/A.cs", "A.Long()", "function_lines", 9),
            new("src/Api/A.cs", "A.Gone()", "function_lines", 8)]);

        var result = await Apply(repo, PrinciplesMode.Refreshed, oldPrinciples: Limits(4));

        result.Should().Be(new StructureBaselineResult(true, 1, 0, true));
        StructureBaselineFile.Parse(repo.Files[Baseline]).Should().ContainSingle()
            .Which.Value.Should().Be(6, "the entry is lowered to what is measured now");
    }

    [Fact]
    public async Task StructureBaseline_Refresh_AddsOnlyUnitsALoweredLimitExposes()
    {
        // Old limit 10, new limit 4: Exposed (6 lines) was green before; Grown (12) passed an old limit.
        var repo = Repo(Class(Method("Exposed", 3), Method("Grown", 9)));
        repo.Files[Baseline] = StructureBaselineFile.Render([]);

        var result = await Apply(repo, PrinciplesMode.Refreshed, oldPrinciples: Limits(10));

        StructureBaselineFile.Parse(repo.Files[Baseline]).Select(e => e.Key).Should().Equal("A.Exposed()");
        result.Sentence().Should().Contain("a unit grown past an unchanged limit surfaces at the gate");
    }

    [Fact]
    public async Task StructureBaseline_Refresh_AddsUnitsOfAMetricTheOldPrinciplesDidNotLimit()
    {
        var repo = Repo(Class(Method("Exposed", 3), Method("Large", 9)));
        repo.Files[Baseline] = StructureBaselineFile.Render([]);

        await Apply(repo, PrinciplesMode.Refreshed, oldPrinciples: "## Additions\n");

        StructureBaselineFile.Parse(repo.Files[Baseline]).Select(e => e.Key)
            .Should().BeEquivalentTo("A.Exposed()", "A.Large()");
    }

    [Fact]
    public async Task StructureBaseline_RefreshThatStops_SaysTheExistingFileStands()
    {
        var repo = Repo(Class(Method("Long", 5)), principles: "## Additions\n");
        repo.Files[Baseline] = StructureBaselineFile.Render([]);

        var sentence = (await Apply(repo, PrinciplesMode.Refreshed)).Sentence();

        sentence.Should().StartWith("structure baseline not refreshed — principles.md declares no Limits")
            .And.Contain("left as it was");
    }
}
