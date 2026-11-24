using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>
/// 2026-11-24-6d76a: the source files no formatter touched are said with their context and why —
/// a structure measurement over unformatted lines is never a silent skip.
/// </summary>
public sealed class SpecFormatGapsTests
{
    [Fact]
    public void SpecFormatGaps_ContextWithoutFormat_IsNamedWithItsCount()
    {
        var api = FormatFixture.Context("api", "src", null);

        var lines = FormatFixture.Gaps().Lines("repo", [api], [], [api], ["src/A.cs", "src/B.cs"]);

        lines.Should().ContainSingle().Which.Should().Be(
            "format: api declares no format in repo; 2 changed source file(s) are committed as written, unformatted");
    }

    [Fact]
    public void SpecFormatGaps_WhenPresentAbsent_SaysThePath()
    {
        var web = FormatFixture.Context("web", "web", null) with
        {
            Format = new ContextYamlFormat("web/node_modules/.bin/prettier --write {files}", "web/node_modules/.bin/prettier"),
        };

        var lines = FormatFixture.Gaps().Lines("repo", [web], [], [web], ["web/src/lib.rs"]);

        lines.Should().ContainSingle().Which.Should().Contain("the web format needs 'web/node_modules/.bin/prettier', absent in repo")
            .And.Contain("1 changed source file(s)");
    }

    [Fact]
    public void SpecFormatGaps_FileNoEngineMeasures_IsNotCounted()
    {
        var api = FormatFixture.Context("api", ".", null);

        var lines = FormatFixture.Gaps().Lines(
            "repo", [api], [], [api], ["README.md", "docs/notes.txt", ".agentsmith/runs/r1/plan.cs"]);

        lines.Should().BeEmpty("only a file the structure engine measures is measured as written, and run records are not code");
    }

    [Fact]
    public void SpecFormatGaps_FileFormattedBySharedWorkdirContext_IsNotCounted()
    {
        var one = FormatFixture.Context("one", "src", "fmt {files}");
        var two = FormatFixture.Context("two", "src", null);

        var lines = FormatFixture.Gaps().Lines("repo", [one, two], [one], [one, two], ["src/A.cs"]);

        lines.Should().BeEmpty("a context sharing the workdir formatted the file");
    }

    [Fact]
    public void SpecFormatGaps_FileOfAnotherSandboxContext_SaysItsFormatDoesNotRunHere()
    {
        var api = FormatFixture.Context("api", "api", null);
        var web = FormatFixture.Context("web", "web", "fmt {files}");

        var lines = FormatFixture.Gaps().Lines("repo", [api], [], [api, web], ["web/Page.cs", "api/A.cs"]);

        lines.Should().Equal(
            "format: api declares no format in repo; 1 changed source file(s) are committed as written, unformatted",
            "format: web files were changed in repo, where its format does not run; 1 changed source file(s) are committed as written, unformatted");
    }

    [Fact]
    public void SpecFormatGaps_NestedContextWithoutFormat_IsNamedNotItsParent()
    {
        var root = FormatFixture.Context("root", ".", "fmt {files}");
        var tool = FormatFixture.Context("tool", "tools/gen", null);

        var lines = FormatFixture.Gaps().Lines(
            "repo", [root, tool], [root], [root, tool], ["src/A.cs", "tools/gen/G.cs"]);

        lines.Should().ContainSingle().Which.Should().StartWith("format: tool declares no format",
            "the nested context owns its file, and the root's formatter never reaches it");
    }

    [Fact]
    public void SpecFormatGaps_FileUnderAnUnformattedContextsSources_CountsForIt()
    {
        var api = FormatFixture.Context("api", "src/Api", null) with { Sources = ["src/Core"] };

        var lines = FormatFixture.Gaps().Lines("repo", [api], [], [api], ["src/Core/B.cs"]);

        lines.Should().ContainSingle().Which.Should().StartWith("format: api declares no format");
    }

    [Fact]
    public async Task SpecFormatRun_FormatterFails_SaysTheFailureNotAGap()
    {
        var sandbox = new FormatSandbox { ShellExitCode = 1 };
        sandbox.Changed.Add("src/A.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", "fmt {files}"));

        var note = await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        note.Should().Contain("api formatter exited 1").And.NotContain("declares no format");
    }

    [Fact]
    public async Task SpecFormatRun_NoContextDeclaresFormat_SaysSo()
    {
        var sandbox = new FormatSandbox();
        sandbox.Changed.Add("src/Client.cs");
        var pipeline = FormatFixture.Pipeline(sandbox, FormatFixture.Context("api", ".", null));

        var note = await FormatFixture.Run().RunAsync(pipeline, CancellationToken.None);

        note.Should().Be(
            "format: api declares no format in repo; 1 changed source file(s) are committed as written, unformatted");
        sandbox.ShellCommands.Should().BeEmpty("no formatter is declared, so none runs");
    }
}
