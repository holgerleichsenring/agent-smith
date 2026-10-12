using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-fbbdd: a diff is read as git means it. An account failed "no changed line
/// exceeds 150 characters" on lines the branch had REMOVED — nothing had told it that a '-'
/// line is gone. And the mechanical readers found headers where git writes none.
/// </summary>
public sealed class DiffReadingTests
{
    [Fact]
    public void SpecAccountPrompt_ExplainsDiffReading()
    {
        var prompt = SpecAccountPrompt.For(
            ["no changed line exceeds 150 characters"], "diff --git a/src/A.cs b/src/A.cs\n", []);

        prompt.Should().Contain(AccountEvidenceRules.DiffReading.TrimEnd());
        prompt.Should().Contain("judged from the '+' lines alone");
        prompt.Should().Contain("proves only what was removed, replaced, renamed or moved");
        prompt.IndexOf("'-' line", StringComparison.Ordinal)
            .Should().BeLessThan(prompt.IndexOf("\nDIFF\n", StringComparison.Ordinal), "the rule comes before the diff");
    }

    [Fact]
    public void SpecReviewPrompt_ExplainsDiffReading()
    {
        var prompt = SpecReviewPrompt.For(
            new SpecDraft("2026-10-12-0000", "goal", "spec: 2026-10-12-0000", []), null,
            [new SpecDiff("api", "diff --git a/src/A.cs b/src/A.cs\n", ["src/A.cs"], [], "main")], null);

        prompt.Should().Contain(AccountEvidenceRules.DiffReading.TrimEnd());
        prompt.Should().Contain("never a '-' line");
        prompt.Should().NotContain("judged from the '+' lines alone", "the account's judging rule is its own");
    }

    [Fact]
    public void DiffWindows_AddedLineContainingTheHeader_StaysInItsFile()
    {
        var a = "diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n@@ -1 +1,2 @@\n"
            + "+var text = \"diff --git a/fake b/fake\";\n" + new string('x', 200) + "\n";
        var b = "diff --git a/src/B.cs b/src/B.cs\n--- a/src/B.cs\n+++ b/src/B.cs\n@@ -1 +1 @@\n"
            + new string('y', 100) + "\n";
        var budget = Math.Max(a.Length, b.Length) + 10;

        var windows = DiffWindows.Split(a + b, budget);

        windows.Should().Equal([a, b], "each real file is its own window, and A's added line stays in A");
    }

    [Fact]
    public void CitedFileIndex_RemovedLineStartingWithDashes_IsNoPath()
    {
        const string diff = "diff --git a/db/init.sql b/db/init.sql\n--- a/db/init.sql\n+++ b/db/init.sql\n"
            + "@@ -1,2 +1 @@\n--- seed the lookup table\n SELECT 1;\n";

        CitedFileIndex.FromDiff(diff).Paths.Should().BeEquivalentTo(["db/init.sql"]);
    }
}
