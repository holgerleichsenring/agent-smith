using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0469: the prompt could not close an ABSENCE criterion, so wiring the agent's commands
/// into the evidence would have changed nothing.
/// <para>
/// It let a command close a build or test result only, and required it to have exited 0. A
/// claim that something is absent everywhere has no file in the diff to name, and the search
/// that proves it exits non-zero precisely when it succeeds. A live run was refused on "owned
/// files contain no MediatR references — no listed command provides an exhaustive scan" while
/// the agent had run that scan several times.
/// </para>
/// </summary>
public sealed class SpecAccountPromptTests
{
    private static string Prompt() => SpecAccountPrompt.For(
        ["no owned file references the legacy library"],
        "diff --git a/src/Program.cs b/src/Program.cs\n",
        ["api: the agent ran 'grep -rn Legacy src' exited 1 — no output"]);

    [Fact]
    public void SpecAccountPrompt_AbsenceCriterion_TellsTheReaderASearchAnswersIt()
    {
        var prompt = Prompt();

        prompt.Should().Contain("ABSENT", "a criterion about an absence is not answerable from a diff");
        prompt.Should().Contain("exits non-zero because it",
            "the search that proves an absence fails by finding nothing, and that is the proof");
        prompt.Should().Contain("could not run", "a search that never ran proves nothing");
        prompt.Should().NotContain("satisfied when a listed command covers it and exited 0",
            "the old exit-status rule is AMENDED, not left beside its replacement");
    }

    [Fact]
    public void SpecAccountPrompt_AbsenceCriterion_RequiresCoverageAtLeastTheCriterions()
    {
        var prompt = Prompt();

        prompt.Should().Contain("reach must be at least the",
            "a repository-wide criterion is not closed by a search of one directory");
        prompt.Should().Contain("one directory or one file glob");
        prompt.Should().Contain("went unsearched", "the gap is named rather than assumed away");
        prompt.Should().Contain("the DIFF wins",
            "the agent's commands ran at arbitrary points; the diff is the final state");
    }
}
