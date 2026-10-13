using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureBaselineRepo;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-50c7: a context's baseline covers its sources, records the directories it owned,
/// and a directory it newly owns is green on the day it is owned.
/// </summary>
public sealed class StructureBaselineSourcesTests
{
    private static StructureBaselineFile File => StructureBaselineStubs.File;

    private static IEnumerable<string> Keys(RepoSandbox repo) => File.Parse(repo.Files[Baseline]).Select(e => e.Key);

    private static string Long(string type, string name) => $"class {type}\n{{\n{Method(name, 5)}}}\n";

    private static RepoSandbox Library()
    {
        var repo = Repo(Long("A", "Api"));
        repo.Files["src/Core/B.cs"] = Long("B", "Core");
        repo.Files["src/Other/C.cs"] = Long("C", "Other");
        return repo;
    }

    private static StructureBaselineScope Scope(PrinciplesMode mode, string[] sources, string? before = null,
        params StructureClaim[] siblings) =>
        new("api", "src/Api", siblings, mode) { Sources = sources, WorkdirBefore = before };

    private static Task<StructureBaselineResult> Apply(RepoSandbox repo, StructureBaselineScope scope) =>
        StructureBaselineStubs.Real().ApplyAsync(repo, scope, CancellationToken.None);

    [Fact]
    public async Task StructureBaseline_Sources_AreListedAndMeasured()
    {
        var repo = Library();

        var result = await Apply(repo, Scope(PrinciplesMode.Transferred, ["src/Core"]));

        result.Entries.Should().Be(2);
        Keys(repo).Should().Equal("A.Api()", "B.Core()");
        repo.Files[Baseline].Should().Contain("# owned:\tsrc/Api\tsrc/Core\n");
        repo.RanSteps.Should().Contain(s => s.Args != null && s.Args.Contains("src/Core/*.cs"));
    }

    [Fact]
    public async Task StructureBaseline_ReinitWithNewSources_AddsTheNewlyOwnedExcess()
    {
        // The old baseline owned src/Api only (no owned line, the context.yaml before said src/Api); the
        // limit did not change, so without the new ownership a refresh would add nothing.
        var repo = Library();
        repo.Files["src/Api/D.cs"] = Long("D", "Grown");
        repo.Files[Baseline] = File.Render([new("src/Api/A.cs", "A.Api()", "function_lines", 8)], LimitSet(4));

        await Apply(repo, Scope(PrinciplesMode.Refreshed, ["src/Core"], before: "src/Api"));

        Keys(repo).Should().Equal(["A.Api()", "B.Core()"], "D grew past an unchanged limit in a directory owned before");
        File.MeasuredWith(repo.Files[Baseline]).Should().BeEquivalentTo(LimitSet(4));
    }

    [Fact]
    public async Task StructureBaseline_PreservedWithNewSources_AppendsOnlyTheNewlyOwnedExcess()
    {
        var repo = Library();
        var kept = new StructureBaselineEntry("src/Api/Gone.cs", "Gone.Run()", "function_lines", 40);
        repo.Files[Baseline] = File.Render([kept], LimitSet(10));

        var result = await Apply(repo, Scope(PrinciplesMode.PreservedExisting, ["src/Core"], before: "src/Api"));

        result.Written.Should().BeTrue();
        Keys(repo).Should().Equal("Gone.Run()", "B.Core()");
        File.MeasuredWith(repo.Files[Baseline]).Should().BeEquivalentTo(LimitSet(10), "the preserved Limits line stays");
        repo.Files[Baseline].Should().Contain("# owned:\tsrc/Api\tsrc/Core");
    }

    [Fact]
    public async Task StructureBaseline_PreservedOwningTheSameDirectories_IsUntouched()
    {
        var repo = Library();
        var kept = File.Render([], LimitSet(4), ["src/Api", "src/Core"]);
        repo.Files[Baseline] = kept;

        var result = await Apply(repo, Scope(PrinciplesMode.PreservedExisting, ["src/Core"]));

        result.Should().Be(StructureBaselineResult.Untouched(0));
        repo.Files[Baseline].Should().Be(kept);
    }

    [Fact]
    public async Task StructureBaseline_HeaderlessBaseline_OwnedTheWorkdirBefore()
    {
        // The context.yaml before the round put the workdir at src/Core: that directory is not new.
        var repo = Library();
        repo.Files[Baseline] = File.Render([], LimitSet(4));

        await Apply(repo, Scope(PrinciplesMode.Refreshed, ["src/Core"], before: "src/Core"));

        Keys(repo).Should().Equal("A.Api()");
    }

    [Fact]
    public async Task StructureBaseline_SourceTiedWithAnEarlierRound_IsMeasured()
    {
        // 'abc' recorded src/Core in an earlier round and owns it by name; 'api' ties on it and measures
        // it anyway, so whichever baseline the gate reads holds the entry.
        var repo = Library();

        await Apply(repo, Scope(PrinciplesMode.Transferred, ["src/Core"], null,
            new StructureClaim("abc", "src/Web", true), new StructureClaim("abc", "src/Core", false)));

        Keys(repo).Should().Contain("B.Core()");
    }

    [Fact]
    public async Task StructureBaseline_SiblingSourceDeeperThanTheWorkdir_IsLeftToIt()
    {
        var repo = Library();
        repo.Files["src/Api/Generated/G.cs"] = Long("G", "Gen");

        await Apply(repo, Scope(PrinciplesMode.Transferred, [], null,
            new StructureClaim("gen", "tools/Gen", true), new StructureClaim("gen", "src/Api/Generated", false)));

        Keys(repo).Should().Equal("A.Api()");
    }

    [Fact]
    public void StructureBaselineOwnedLine_RenderedLine_ReadsBack()
    {
        var line = new StructureBaselineOwnedLine();

        line.Read("# path\n" + line.Render(["", "src/Core"]) + "\n").Should().Equal("", "src/Core");
        line.Read("# path\n").Should().BeNull();
    }
}
