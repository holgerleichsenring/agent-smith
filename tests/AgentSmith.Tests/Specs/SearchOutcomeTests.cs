using AgentSmith.Application.Services.Specs;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-24-485e: a search result that is cut has to say so in a way a reader cannot mistake
/// for an inventory. A live premise check read "the lockfile is under &lt;dir&gt;/ rather than at the
/// repository root" out of a match list whose tail it never saw — the pattern matched thousands
/// of lines inside the lockfiles, and the list ended in tree-walk order long before the root.
/// </summary>
public sealed class SearchOutcomeTests
{
    private static StepResult Exit(int code, string? output = null) => new(
        StepResult.CurrentSchemaVersion, Guid.NewGuid(), code, TimedOut: false,
        DurationSeconds: 0, ErrorMessage: null, OutputContent: output);

    [Fact]
    public void OutputBeyondTheLimit_NamesHowMuchWasShownAndThatItIsAHead()
    {
        var report = SearchOutcome.Report(Exit(0, new string('x', 12_000)), "repo", null, "pattern");

        report.Should().Contain("cut here").And.Contain("4000 characters");
        report.Should().Contain("HEAD, not an inventory");
        report.Should().Contain("nothing is absent because it is not above");
    }

    [Fact]
    public void OutputWithinTheLimit_IsReturnedWholeWithNoCutNotice()
    {
        var report = SearchOutcome.Report(Exit(0, "one match"), "repo", null, "pattern");

        report.Should().Contain("one match").And.NotContain("cut here");
    }

    // grep exits 1 BECAUSE it found nothing, which is the proof an absence asks for — and it
    // must stay worded far apart from a search that could not run at all.
    [Fact]
    public void NothingFound_SaysItDoesNotOccurRatherThanThatNothingRan()
    {
        var report = SearchOutcome.Report(Exit(1), "repo", "sub", "pattern");

        report.Should().Contain("does not occur anywhere in repo/sub").And.NotContain("proves nothing");
    }
}
