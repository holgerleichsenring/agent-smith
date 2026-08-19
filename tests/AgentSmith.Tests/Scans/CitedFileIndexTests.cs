using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: one index, one matching rule, two sources of evidence. A phase brings the
/// branch diff; a scan brings the files it read. Cloning the rule for the second would
/// let a scan finding and a phase account disagree about what "cited" means.
/// </summary>
public sealed class CitedFileIndexTests
{
    private const string Diff = """
        diff --git a/src/Api/Swagger.cs b/src/Api/Swagger.cs
        --- a/src/Api/Swagger.cs
        +++ b/src/Api/Swagger.cs
        @@ -1 +1 @@
        -old
        +new
        """;

    [Fact]
    public void CitedFileIndex_FromPaths_ResolvesACitationTheSameWayAsFromDiff()
    {
        var fromDiff = CitedFileIndex.FromDiff(Diff);
        var fromPaths = CitedFileIndex.FromPaths(["src/Api/Swagger.cs"]);

        foreach (var citation in new[] { "src/Api/Swagger.cs", "Swagger.cs", "Swagger.cs:44" })
            fromPaths.Contains(citation).Should().Be(fromDiff.Contains(citation), citation);
        fromPaths.Contains("src/Api/Imagined.cs").Should().Be(fromDiff.Contains("src/Api/Imagined.cs"));
    }

    [Fact]
    public void CitedFileIndex_FromNoPaths_IsEmptyAndResolvesNothing()
    {
        var index = CitedFileIndex.FromPaths(null);

        index.IsEmpty.Should().BeTrue();
        index.Contains("anything.cs").Should().BeFalse();
    }

    [Fact]
    public void CitationResolver_OverAScannedFileList_SatisfiesACriterionCitingAReadFile()
    {
        var resolver = new CitationResolver(CitedFileIndex.FromPaths(["src/Api/Swagger.cs"]), []);

        var account = resolver.Resolve(new AccountRow("the spec is documented", true, "Swagger.cs"));

        account.Satisfied.Should().BeTrue();
        account.Citation.Should().Be("Swagger.cs");
    }

    [Fact]
    public void CitationResolver_OverACommandThatRan_MarksTheAnswerMechanical()
    {
        var resolver = new CitationResolver(
            CitedFileIndex.FromPaths([]), ["DependencyAuditCommand: 0 advisories"]);

        var account = resolver.Resolve(
            new AccountRow("dependencies audited", true, "DependencyAuditCommand"));

        account.Satisfied.Should().BeTrue();
        account.Mechanical.Should().BeTrue("a command's answer is evidence of a different kind");
    }

    /// <summary>
    /// p0469: the check matched a citation against the WHOLE evidence line, output included,
    /// in both directions. That was tolerable over two build lines and degrades toward
    /// always-true over forty agent commands carrying four hundred characters of output each
    /// — a model could close a criterion by quoting something it read in a command's output.
    /// </summary>
    [Fact]
    public void CitationResolver_CitationMatchingOnlyCommandOutput_DoesNotResolve()
    {
        var resolver = new CitationResolver(
            CitedFileIndex.FromPaths([]),
            ["api: the agent ran 'grep -rn Legacy src' exited 1 — output: no matches in src/Sample.cs"]);

        var account = resolver.Resolve(
            new AccountRow("no owned file references the legacy library", true, "no matches in src/Sample.cs"));

        account.Satisfied.Should().BeFalse("a string from a command's output is not the command");
    }

    /// <summary>The command itself still resolves — the check is narrowed, not closed.</summary>
    [Fact]
    public void CitationResolver_CitationNamingTheAgentsOwnSearch_StillResolves()
    {
        var resolver = new CitationResolver(
            CitedFileIndex.FromPaths([]),
            ["api: the agent ran 'grep -rn Legacy src' exited 1 — no output"]);

        var account = resolver.Resolve(
            new AccountRow("no owned file references the legacy library", true, "grep -rn Legacy src"));

        account.Satisfied.Should().BeTrue();
        account.Mechanical.Should().BeTrue();
    }

    [Fact]
    public void CitedCodeWindow_NumbersTheLinesAroundTheCitation()
    {
        var window = new CitedCodeWindow().Around("a\nb\nc\nd\n", 2);

        window.Should().Contain("     1: a").And.Contain("     2: b");
    }

    [Fact]
    public void CitedCodeWindow_LineBeyondTheFile_SaysSoRatherThanInventing()
    {
        new CitedCodeWindow().Around("a\nb\n", 400).Should().Contain("does not exist");
    }
}
