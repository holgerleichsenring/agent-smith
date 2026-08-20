using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0481: run 2026-08-20T09-03-57-0944 built both repositories, passed 2,235 tests, pushed
/// and opened two pull requests, and was refused at the gate over ONE apostrophe. The command
/// was cut at exactly 200 characters, the cut landed inside the literal
/// <c>echo 'No MassTransit references found'</c>, the line showed an unbalanced quote and the
/// account closed it — 202 characters cited against the 201 the line carried.
/// </summary>
public sealed class ShortenedCommandCitationTests
{
    private const string Scan =
        "set -e\nprintf '%s\\n' '=== MassTransit source/project scan ==='\n"
        + "if grep -RIn 'MassTransit' --include='*.cs' --include='*.csproj' "
        + "--exclude-dir=bin --exclude-dir=obj .; then exit 1; "
        + "else echo 'No MassTransit references found in owned sources or projects'; fi";

    /// <summary>Built from what the log actually stores, never from a frozen literal: the
    /// rendering this phase changes would otherwise be pinned by the test that proves it.
    /// </summary>
    private static string EvidenceLine()
    {
        var log = new PhaseCommandLog();
        log.Record("Sample.Server", Scan, "exit_code: 0\n\nstdout:\nNo MassTransit references found");
        return log.Evidence().Single();
    }

    private static string ShownCommand() => EvidenceCommand.InEvidence(EvidenceLine());

    private static CriterionAccount Resolve(string citation, params string[] commands) =>
        new CitationResolver(CitedFileIndex.FromDiff(string.Empty), commands)
            .Resolve(new AccountRow("criterion", true, citation, "note"));

    [Fact]
    public void Citation_TheShownCommandPlusTheGrammarsClosingQuote_Resolves()
    {
        var line = EvidenceLine();

        Resolve(EvidenceCommand.InEvidence(line) + "'", line).Satisfied.Should().BeTrue(
            "a cut inside a shell literal leaves the line an unbalanced quote and the reader closes it");
    }

    [Fact]
    public void Citation_TheShownCommandCopiedWhole_Resolves()
    {
        var line = EvidenceLine();

        Resolve(EvidenceCommand.InEvidence(line), line).Satisfied.Should().BeTrue();
    }

    /// <summary>
    /// The natural move when the middle is elided, and it is refused. Two commands of the
    /// same family share their head by construction and differ only in the paths they ran
    /// against — run 0944 issued 21 over-cap commands of exactly that shape — so a head-only
    /// citation names both and closes a two-repository criterion with a one-repository
    /// search. That is the refusal this phase exists to end, inverted into a false positive.
    /// </summary>
    [Fact]
    public void Citation_OnlyTheVisibleHeadOfAShortenedCommand_DoesNotResolve()
    {
        var line = EvidenceLine();
        var head = ShownCommand().Split('…')[0];

        Resolve(head, line).Satisfied.Should().BeFalse(
            "a head does not tell two sibling commands apart, and the tail is what does");
    }

    /// <summary>The proof of the finding above, in the shape the failing run produced: one
    /// search per repository, identical but for the path.</summary>
    [Fact]
    public void Citation_TwoSearchesDifferingOnlyInTheirPath_AreNotBothNamedByTheirSharedHead()
    {
        var log = new PhaseCommandLog();
        var flags = new string('f', 200); // enough that the command is shortened, which is the case at issue
        log.Record("Sample.Server", $"grep -RIn 'LocalQueue' {flags} /work/Sample.Server/src", "exit_code: 1");
        log.Record("Sample.Worker", $"grep -RIn 'LocalQueue' {flags} /work/Sample.Worker/src", "exit_code: 1");

        var lines = log.Evidence();
        var shared = EvidenceCommand.InEvidence(lines[0]).Split('…')[0];

        Resolve(shared, [.. lines]).Satisfied.Should().BeFalse(
            "the head both searches share names neither of them in particular");
        Resolve(EvidenceCommand.InEvidence(lines[1]), [.. lines]).Satisfied.Should().BeTrue(
            "the whole shown command names exactly one of them");
    }

    /// <summary>A model that copies the whole evidence line rather than the quoted span goes
    /// through the other reading, which is trimmed the same way.</summary>
    [Fact]
    public void Citation_AWholeEvidenceLineOfAShortenedCommand_Resolves()
    {
        var line = EvidenceLine();

        Resolve(line, line).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Citation_TrimmingTheQuotesLeavesNothing_DoesNotResolve()
    {
        Resolve("'''", EvidenceLine()).Satisfied.Should().BeFalse(
            "trimming shortens a citation, it never invents one");
    }

    /// <summary>The property the trim must not spend: a citation that names a command nobody
    /// ran still fails, quotes or no quotes.</summary>
    [Fact]
    public void Citation_ACommandThatNeverRan_StillDoesNotResolve()
    {
        Resolve("rm -rf /'", EvidenceLine()).Satisfied.Should().BeFalse();
    }

    [Fact]
    public void Citation_AShortPrefixOfARealCommand_StillDoesNotResolve()
    {
        Resolve("set -e'", EvidenceLine()).Satisfied.Should().BeFalse(
            "p0473's floor keeps 'dotnet' from standing in for 'dotnet test'");
    }
}
