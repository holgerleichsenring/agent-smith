using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;
using static AgentSmith.Tests.Structure.StructureBaselineRepo;

namespace AgentSmith.Tests.Structure;

public sealed class StructureBaselineTests
{
    [Fact]
    public async Task StructureBaseline_FirstInit_WritesEveryUnitOverItsLimit()
    {
        var repo = Repo(Class(Method("Long", 5), Method("Short", 1)));

        var result = await Apply(repo);

        result.Should().Be(new StructureBaselineResult(true, 1, 0, false));
        StructureBaselineStubs.File.Parse(repo.Files[Baseline]).Should().ContainSingle()
            .Which.Should().Be(new StructureBaselineEntry("src/Api/A.cs", "A.Long()", "function_lines", 8));
    }

    [Fact]
    public async Task StructureBaseline_PreservedWithoutBaseline_WritesIt()
    {
        var repo = Repo(Class(Method("Long", 5)));

        (await Apply(repo, PrinciplesMode.PreservedExisting)).Written.Should().BeTrue();
        repo.Files.Should().ContainKey(Baseline);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StructureBaseline_PreservedWithBaseline_LeavesIt(bool limited)
    {
        var repo = Repo(Class(Method("Long", 5)), limited ? Limits(4) : "## Additions\n");
        var kept = StructureBaselineStubs.File.Render([new("src/Api/A.cs", "A.Other()", "function_lines", 50)], LimitSet(4));
        repo.Files[Baseline] = kept;

        var result = await Apply(repo, PrinciplesMode.PreservedExisting);

        result.Should().Be(StructureBaselineResult.Untouched(1), "the preserved file's own baseline is what stands");
        result.Sentence().Should().Contain("left untouched");
        repo.Files[Baseline].Should().Be(kept);
    }

    [Fact]
    public async Task StructureBaseline_SkillWrites_WritesNothingAndSaysSo()
    {
        var repo = Repo(Class(Method("Long", 5)));

        (await Apply(repo, PrinciplesMode.SkillWrites)).Note.Should().Contain("bootstrap skill wrote principles.md");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_NoMeasuredLanguage_WritesNothingAndSaysSo()
    {
        var repo = new RepoSandbox();
        repo.Files[Principles] = Limits(4);
        repo.Files["src/Api/README.md"] = "# api\n";

        var result = await Apply(repo);

        result.Note.Should().Contain("no tracked file under the workdir is in a measured language");
        repo.Files.Should().NotContainKey(Baseline, "an empty baseline would claim nothing exceeds a limit");
    }

    [Fact]
    public async Task StructureBaseline_ListingAtCaptureCap_WritesNothingAndSaysSo()
    {
        // 2026-10-13-2d0c: a path with a newline makes the capture two lines, and the second may be cut.
        var repo = Repo(Class(Method("Long", 5)));
        repo.Listing = new string('a', 500_000) + "\n" + new string('b', 500_000) + "\0\n";

        (await Apply(repo)).Note.Should().Contain("capture cap");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Theory]
    [InlineData("/work/src")]
    [InlineData("../other")]
    public async Task StructureBaseline_RootedWorkdir_WritesNothing(string workdir)
    {
        var repo = Repo(Class(Method("Long", 5)));

        (await Apply(repo, workdir: workdir)).Note.Should().Contain("rooted or escapes");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_NoLimitsBlock_WritesNothing()
    {
        var repo = Repo(Class(Method("Long", 5)), principles: "## Additions\n");

        (await Apply(repo)).Sentence().Should().Be("no structure baseline — principles.md declares no Limits");
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_PathOfNestedComponent_IsLeftToIt()
    {
        var repo = Repo(Class(Method("Long", 5)));
        repo.Files["src/Api/Worker/B.cs"] = "class B\n{\n" + Method("Long", 5) + "}\n";

        var result = await Apply(repo, siblings: "src/Api/Worker");

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

    [Fact]
    public async Task StructureBaseline_GrammarUnavailable_WritesNothingAndSaysSo()
    {
        var repo = Repo(Class(Method("Long", 5)));

        var result = await Apply(repo, engine: StructureEngineFixture.MissingGrammar());

        result.Written.Should().BeFalse();
        result.Note.Should().NotBeNullOrEmpty();
        repo.Files.Should().NotContainKey(Baseline);
    }

    [Fact]
    public async Task StructureBaseline_WriteFails_ReturnsTheReasonInsteadOfThrowing()
    {
        var repo = Repo(Class(Method("Long", 5)));
        repo.FailingWrites.Add(Baseline);

        (await Apply(repo)).Note.Should().Contain("measuring or writing it failed");
    }
}
