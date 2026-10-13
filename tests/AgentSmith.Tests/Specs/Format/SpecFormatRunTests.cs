using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Structure;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>
/// 2026-10-12-fbbdb: a context's formatter runs over the files THIS spec changed under that
/// context's workdir — shell-quoted, repository-root-relative, run records left out.
/// </summary>
public sealed class SpecFormatRunTests
{
    [Fact]
    public async Task SpecFormatRun_FilesPlaceholder_GetsChangedPathsOfItsWorkdir()
    {
        var sandbox = new FormatSandbox();
        sandbox.Changed.AddRange(
            ["api/src/A.cs", "api/src/it's.cs", "web/app.ts", ".agentsmith/runs/r1/plan.md", "README.md"]);
        var pipeline = FormatFixture.Pipeline(sandbox,
            FormatFixture.Context("api", "api", "dotnet csharpier {files}"),
            FormatFixture.Context("web", "web", null),
            FormatFixture.Context("root", ".", "prettier --write {files}"));

        var note = await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        note.Should().BeNull("every formatter exited 0");
        sandbox.ShellCommands.Should().BeEquivalentTo(
            "dotnet csharpier './api/src/A.cs' './api/src/it'\\''s.cs'",
            "prettier --write './README.md'");
        sandbox.RanSteps.Where(FormatSandbox.IsShell).Should().OnlyContain(step => step.WorkingDirectory == "/work",
            "a declared command runs at the repository root");
        sandbox.RanSteps.Should().Contain(step => FormatSandbox.IsGit(step, FormatFixture.StartHead),
            "the changed files are measured from where THIS spec started");
    }

    [Fact]
    public async Task SpecFormatRun_SameCommandAndFiles_RunsOnce()
    {
        var sandbox = new FormatSandbox();
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox,
            FormatFixture.Context("one", "src", "dotnet csharpier {files}"),
            FormatFixture.Context("two", "src", "dotnet csharpier {files}"));

        await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        sandbox.ShellCommands.Should().ContainSingle();
    }

    [Fact]
    public async Task SpecFormatRun_NoStartHead_RunsNothing()
    {
        var sandbox = new FormatSandbox();
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", "dotnet csharpier {files}"));
        pipeline.Remove(AgentSmith.Contracts.Commands.ContextKeys.SpecStartHeads);

        var note = await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        note.Should().BeNull();
        sandbox.ShellCommands.Should().BeEmpty("without a start head nothing says which files are this spec's");
    }

    [Fact]
    public async Task SpecFormatRun_FormatterMissing_ReturnsNoteNeverThrows()
    {
        var sandbox = new FormatSandbox { ShellExitCode = 127 };
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", "dotnet csharpier {files}"));

        var note = await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        note.Should().Contain("api formatter exited 127").And.Contain("committed as written");
    }

    [Fact]
    public void SpecFormatPlan_PathOfANestedContext_GoesToThatContextOnly()
    {
        var plan = new SpecFormatPlan(new StructureOwnership()).Plan(
            [FormatFixture.Context("root", ".", "fmt {files}")],
            [FormatFixture.Context("web", "web", null)],
            ["web/app.ts", "src/A.cs"]);

        plan.Should().ContainSingle().Which.CommandLine.Should().Be("fmt './src/A.cs'");
    }

    [Fact]
    public void SpecFormatPlan_DashLeadingPath_IsPassedAsRelative()
    {
        var plan = new SpecFormatPlan(new StructureOwnership()).Plan(
            [FormatFixture.Context("root", ".", "fmt {files}")], [], ["-rf.cs", "--help.cs"]);

        plan.Should().ContainSingle().Which.CommandLine.Should().Be(
            "fmt './-rf.cs' './--help.cs'", "a path that starts with '-' must never reach the formatter as an option");
    }

    [Fact]
    public void SpecFormatPlan_FileUnderSources_IsFormatted()
    {
        // 2026-10-13-50c7: the library the API project references is the API context's to format.
        var api = FormatFixture.Context("api", "src/Api", "fmt {files}") with { Sources = ["src/Core"] };

        var plan = new SpecFormatPlan(new StructureOwnership()).Plan(
            [api], [api], ["src/Api/A.cs", "src/Core/B.cs", "docs/C.cs"]);

        plan.Should().ContainSingle().Which.CommandLine.Should().Be("fmt './src/Api/A.cs' './src/Core/B.cs'");
    }

    [Fact]
    public void SpecFormatPlan_SharedWorkdir_EveryContextFormatsIt()
    {
        var plan = new SpecFormatPlan(new StructureOwnership()).Plan(
            [FormatFixture.Context("one", "src", "fmt-one {files}"), FormatFixture.Context("two", "src", "fmt-two {files}")],
            [], ["src/A.cs"]);

        plan.Select(p => p.CommandLine).Should().Equal("fmt-one './src/A.cs'", "fmt-two './src/A.cs'");
    }
}
