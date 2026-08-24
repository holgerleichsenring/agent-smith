using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0481: run 0944 was refused twice by the same cut. The fixtures here are the commands that
/// run really issued, anonymised — built from short synthetic strings these tests would pass
/// at any head and tail, and the sizes are the whole point.
/// </summary>
public sealed class CommandElisionTests
{
    /// <summary>The absence search the account said it could not see the reach of. Its
    /// pattern sits in the first 130 characters and the PATHS it searched sit past them,
    /// which is what the old end-cut removed.</summary>
    private const string WolverineSearch =
        "grep -RInE 'MassTransit|InvokeAsync|ListenTo|LocalQueue' --include='*.cs' "
        + "--include='*.csproj' --exclude-dir=bin --exclude-dir=obj "
        + "Sample.Infrastructure.Messaging Sample.Application Sample.Api Sample.Domain";

    /// <summary>The command whose citation cost the run: the end-cut landed inside the shell
    /// literal echo 'No MassTransit references found'.</summary>
    private const string MassTransitScan =
        "set -e\nprintf '%s\\n' '=== MassTransit source/project scan ==='\n"
        + "if grep -RIn 'MassTransit' --include='*.cs' --include='*.csproj' "
        + "--exclude-dir=bin --exclude-dir=obj .; then exit 1; "
        + "else echo 'No MassTransit references found in owned sources or projects'; fi";

    [Fact]
    public void CommandElision_ACommandOverTheCap_KeepsItsHeadAndItsTail()
    {
        var shortened = CommandElision.Shorten(WolverineSearch, 200);

        shortened.Should().StartWith("grep -RInE 'MassTransit|InvokeAsync|ListenTo|LocalQueue'")
            .And.Contain("…", "the reader is told something was left out");
        shortened.Should().EndWith("Sample.Application Sample.Api Sample.Domain",
            "the paths a search ran against are what its reach is judged on");
    }

    /// <summary>
    /// The refusal this closes: "no listed successful absence search covers Wolverine
    /// local-queue usage across both repositories". The account judges REACH, and reach is
    /// the pattern, the flags that narrow it and the paths it ran against — head and tail
    /// between them carry all three. Only the middle of the flag list is spent, and this
    /// asserts what actually survives rather than the whole command.
    /// </summary>
    [Fact]
    public void CommandElision_TheLiveWolverineSearch_ShowsItsPatternItsFlagsAndItsPaths()
    {
        var shortened = CommandElision.Shorten(WolverineSearch, 200);

        shortened.Should().Contain("LocalQueue", "the pattern is what the criterion names")
            .And.Contain("--include='*.cs'",
                "a flag that NARROWS a search stays visible, or a narrow search reads as a wide one")
            .And.Contain("Sample.Application Sample.Api Sample.Domain",
                "the paths give the search its reach, and the tail starts at a whole one");
    }

    /// <summary>WHICH repository a search covered is carried by the evidence line, not by the
    /// command: the agent runs one sandbox per repository and the log records the repository
    /// it ran in. Elision cannot take that away, and this pins it.</summary>
    [Fact]
    public void PhaseCommandLog_TheSameSearchInTwoRepositories_NamesEachRepositoryItRanIn()
    {
        var log = new PhaseCommandLog();
        log.Record("Sample.Distribution.Server", WolverineSearch, "exit_code: 1\n\nstdout:\n");
        log.Record("Sample.Distribution.Worker", WolverineSearch, "exit_code: 1\n\nstdout:\n");

        log.Evidence().Should().SatisfyRespectively(
            first => first.Should().StartWith("Sample.Distribution.Server: ").And.Contain("exited 1"),
            second => second.Should().StartWith("Sample.Distribution.Worker: ").And.Contain("exited 1"));
    }

    [Fact]
    public void CommandElision_ACommandUnderTheCap_IsUntouched()
    {
        CommandElision.Shorten("dotnet build Sample.sln", 200).Should().Be("dotnet build Sample.sln");
    }

    [Fact]
    public void CommandElision_ACommandAtExactlyTheCap_IsUntouched()
    {
        var exact = new string('y', 200);

        CommandElision.Shorten(exact, 200).Should().Be(exact);
    }

    [Fact]
    public void CommandElision_OneCharacterOverTheCap_IsShortened()
    {
        CommandElision.Shorten(new string('y', 201), 200).Should().Contain("…").And.HaveLength(200);
    }

    [Fact]
    public void CommandElision_AnElidedCommand_StaysWithinTheCap()
    {
        CommandElision.Shorten(MassTransitScan, 200).Length.Should().BeLessThanOrEqualTo(200);
        CommandElision.Shorten(new string('y', 40_000), 200).Length.Should().BeLessThanOrEqualTo(200);
    }

    /// <summary>A command that spans several lines renders as several items of a list the
    /// prompt writes one item per line, and the reader counts what it can see.</summary>
    /// <summary>
    /// The tail holds 69 characters, and two absolute sandbox paths do not fit in it. What is
    /// guaranteed is that the tail BEGINS at a whole argument, so what survives is true even
    /// when it is not all of it — and WHICH repository a search covered is carried by the
    /// evidence line's repository prefix, not by the paths inside the command.
    /// </summary>
    [Fact]
    public void CommandElision_MorePathsThanTheTailHolds_StillBeginsAtAWholePath()
    {
        var search = "grep -RInE 'MassTransit|InvokeAsync|ListenTo|LocalQueue' --include='*.cs' "
            + "--include='*.csproj' --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git "
            + "/work/Sample.Distribution.Server/src /work/Sample.Distribution.Worker/src";

        var kept = CommandElision.Shorten(search, 200).Split('…')[1];

        kept.Should().Be("/work/Sample.Distribution.Worker/src",
            "one whole path fits and half of another would read as a path it is not");
    }

    /// <summary>Built from short synthetic strings this would pass at any split, so the marker
    /// planted here is at a position only the real head and tail can decide.</summary>
    [Fact]
    public void CommandElision_TheElidedMiddle_IsTheOnlyPartLost()
    {
        var planted = new string('a', 130) + new string('b', 100) + new string('c', 69);

        var shortened = CommandElision.Shorten(planted, 200);

        shortened.Should().Be(new string('a', 130) + "…" + new string('c', 69));
    }

    /// <summary>Trim, then collapse, then measure, then elide. Measuring before the collapse
    /// would elide a command that fits once its whitespace runs are one space each, and
    /// slicing before it would make the head shorter than the 130 the citation floor assumes.
    /// </summary>
    [Fact]
    public void CommandElision_WhitespaceIsCollapsedBeforeTheLengthIsMeasured()
    {
        var padded = "grep -RIn 'x'" + new string(' ', 300) + "src";

        CommandElision.Shorten(padded, 200).Should().Be("grep -RIn 'x' src");
    }

    /// <summary>At most the cap, and less when snapping the tail to a whole argument gives
    /// characters back. Under the cap is safe; over it is what the budget forbids.</summary>
    [Fact]
    public void CommandElision_AWhitespaceHeavyCommandStillOverTheCap_StaysWithinTheCap()
    {
        var padded = string.Join("  ", Enumerable.Repeat(new string('y', 20), 30));

        var shortened = CommandElision.Shorten(padded, 200);

        shortened.Length.Should().BeInRange(180, 200);
        shortened.Should().NotContain("  ", "the collapse runs before anything is measured");
    }

    /// <summary>Half a path printed as though it were a path is evidence that is partly true,
    /// which is what this phase exists to end.</summary>
    [Fact]
    public void CommandElision_TheTail_BeginsAtAWholeArgument()
    {
        var shortened = CommandElision.Shorten(WolverineSearch, 200);

        shortened.Split('…')[1].Should().StartWith("Sample.", "never mid-token");
    }

    [Fact]
    public void CommandElision_NewlinesInACommand_CollapseToSpaces()
    {
        CommandElision.Shorten("set -e\nprintf 'x'\ngrep -RIn 'y' .", 200)
            .Should().Be("set -e printf 'x' grep -RIn 'y' .");
    }

    /// <summary>A path can carry an emoji or a CJK extension, and half a surrogate pair no
    /// longer names the file it came from.</summary>
    [Fact]
    public void CommandElision_ASurrogatePairOnTheBoundary_IsNotSplit()
    {
        var shortened = CommandElision.Shorten(new string('y', 129) + "\U0001F600" + new string('z', 200), 200);

        shortened.Should().NotContain("\uFFFD");
        shortened.Any(char.IsSurrogate).Should().BeFalse("half a surrogate pair names no file");
    }
}
