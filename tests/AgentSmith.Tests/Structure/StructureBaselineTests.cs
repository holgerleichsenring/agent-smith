using AgentSmith.Application.Services.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

public sealed class StructureBaselineTests
{
    private const string Principles = ".agentsmith/contexts/api/principles.md";
    private const string Baseline = ".agentsmith/contexts/api/structure-baseline.tsv";
    private const string Limits = "## Limits\n\n```yaml\nfunction_lines: 4\n```\n";

    private static string Method(string name, int bodyLines) =>
        $"    void {name}()\n    {{\n{string.Concat(Enumerable.Repeat("        x();\n", bodyLines))}    }}\n";

    private static string Class(params string[] methods) => $"class A\n{{\n{string.Concat(methods)}}}\n";

    private static RepoSandbox Repo(string source, string principles = Limits)
    {
        var repo = new RepoSandbox();
        repo.Files[Principles] = principles;
        repo.Files["src/Api/A.cs"] = source;
        return repo;
    }

    private static Task<StructureBaselineResult> Apply(
        RepoSandbox repo, string workdir = "src/Api", bool refresh = false, params string[] siblings) =>
        StructureBaselineStubs.Real().ApplyAsync(
            repo, new StructureBaselineScope("api", workdir, siblings, refresh), CancellationToken.None);

    [Fact]
    public async Task StructureBaseline_FirstInit_WritesEveryUnitOverItsLimit()
    {
        var repo = Repo(Class(Method("Long", 5), Method("Short", 1)));

        var result = await Apply(repo);

        result.Should().Be(new StructureBaselineResult(true, 1, 0, false));
        StructureBaselineFile.Parse(repo.Files[Baseline]).Should().ContainSingle()
            .Which.Should().Be(new StructureBaselineEntry("src/Api/A.cs", "A.Long()", "function_lines", 8));
    }

    [Fact]
    public async Task StructureBaseline_Refresh_DropsFixedAndLowersShrunkEntries()
    {
        var repo = Repo(Class(Method("Long", 3), Method("Gone", 1)));
        repo.Files[Baseline] = StructureBaselineFile.Render([
            new("src/Api/A.cs", "A.Long()", "function_lines", 9),
            new("src/Api/A.cs", "A.Gone()", "function_lines", 8)]);

        await Apply(repo, refresh: true);

        StructureBaselineFile.Parse(repo.Files[Baseline]).Should().ContainSingle()
            .Which.Value.Should().Be(6, "the entry is lowered to what is measured now");
    }

    [Fact]
    public async Task StructureBaseline_Refresh_AddsNoNewEntry()
    {
        var repo = Repo(Class(Method("Grown", 6)));
        repo.Files[Baseline] = StructureBaselineFile.Render([]);

        var result = await Apply(repo, refresh: true);

        result.Entries.Should().Be(0);
        StructureBaselineFile.Parse(repo.Files[Baseline]).Should().BeEmpty();
    }

    [Fact]
    public async Task StructureBaseline_PreservedWithoutBaseline_WritesIt()
    {
        var repo = Repo(Class(Method("Long", 5)));

        (await Apply(repo, refresh: false)).Written.Should().BeTrue();
        repo.Files.Should().ContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_PreservedWithBaseline_LeavesIt()
    {
        var repo = Repo(Class(Method("Long", 5)));
        var kept = StructureBaselineFile.Render([new("src/Api/A.cs", "A.Other()", "function_lines", 50)]);
        repo.Files[Baseline] = kept;

        var result = await Apply(repo, refresh: false);

        result.Written.Should().BeFalse();
        repo.Files[Baseline].Should().Be(kept);
    }

    [Fact]
    public async Task StructureBaseline_ListingAtCaptureCap_WritesNothingAndSaysSo()
    {
        var repo = Repo(Class(Method("Long", 5)));
        repo.Listing = new string('a', 1_000_000) + "\0\n";

        var result = await Apply(repo);

        result.Note.Should().Contain("capture cap");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Theory]
    [InlineData("/work/src")]
    [InlineData("../other")]
    public async Task StructureBaseline_RootedWorkdir_WritesNothing(string workdir)
    {
        var repo = Repo(Class(Method("Long", 5)));

        (await Apply(repo, workdir)).Note.Should().Contain("rooted or escapes");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_NoLimitsBlock_WritesNothing()
    {
        var repo = Repo(Class(Method("Long", 5)), principles: "## Additions\n");

        (await Apply(repo)).Note.Should().Contain("no Limits");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_PathOfNestedComponent_IsLeftToIt()
    {
        var repo = Repo(Class(Method("Long", 5)));
        repo.Files["src/Api/Worker/B.cs"] = "class B\n{\n" + Method("Long", 5) + "}\n";

        var result = await Apply(repo, "src/Api", false, "src/Api/Worker");

        result.Entries.Should().Be(1);
        repo.Files[Baseline].Should().NotContain("Worker");
    }

    [Fact]
    public async Task StructureBaseline_NotAGitCheckout_WritesNothingAndSaysSo()
    {
        var repo = Repo(Class(Method("Long", 5)));
        repo.ListingExitCode = 128;

        (await Apply(repo)).Note.Should().Contain("not a git checkout");
    }
}
