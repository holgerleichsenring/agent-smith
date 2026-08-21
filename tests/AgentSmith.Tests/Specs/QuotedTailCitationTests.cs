using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0492: run 2026-08-21T06-29-48-6697 reached step 42 of 43, spent $8.19 and had the
/// delivery account run 44 searches of its own — and was refused on one criterion, "final
/// restore, build and existing test commands exit successfully for both affected
/// repositories", over a command it had cited correctly and IN FULL.
/// <para>
/// The command ends in <c>--logger "console;verbosity=minimal"</c>. p0481 trims the
/// grammar's own closing quote off the CITATION and off nothing else, so the citation came
/// out one character shorter than the command — and p0481 also requires a shortened command
/// to be cited at exactly its own length. A command whose last character is a quote could
/// therefore never be cited at all once it was long enough to be shortened.
/// </para>
/// </summary>
public sealed class QuotedTailCitationTests
{
    /// <summary>The live verification script, anonymised, at a comparable length — what
    /// matters is that it exceeds the 200-character cap and that its last character is the
    /// closing quote of <c>--logger</c>.</summary>
    private const string Verify =
        "set -e\n"
        + "dotnet restore /work/Sample.Server/src/Sample.Server/Sample.Server.sln\n"
        + "dotnet build /work/Sample.Server/src/Sample.Server/Sample.Server.sln --configuration Release\n"
        + "dotnet test /work/Sample.Server/src/Sample.Server/Sample.Server.sln --configuration Release "
        + "--no-build --logger \"console;verbosity=minimal\"";

    /// <summary>Built from what the log actually stores, never from a frozen literal: a test
    /// that pinned a rendering the code no longer produces would prove nothing.</summary>
    private static string EvidenceLine()
    {
        var log = new PhaseCommandLog();
        log.Record("Sample.Server", Verify, "exit_code: 0\n\nstdout:\nPassed! - Failed: 0, Passed: 226");
        return log.Evidence().Single();
    }

    private static string ShownCommand() => EvidenceCommand.InEvidence(EvidenceLine());

    private static CriterionAccount Resolve(string citation, params string[] commands) =>
        new CitationResolver(CitedFileIndex.FromDiff(string.Empty), commands)
            .Resolve(new AccountRow("criterion", true, citation, "note"));

    /// <summary>The premise of every case below: the fixture really is shortened, and its
    /// last character really is a quote. Without both, the rest proves nothing.</summary>
    [Fact]
    public void TheFixture_IsShortenedAndEndsInAQuote()
    {
        var shown = ShownCommand();

        shown.Should().Contain("…", "the command must exceed the cap for p0481's full-length rule to apply");
        shown.Should().EndWith("\"", "the defect is a command whose own last character is a quote");
    }

    /// <summary>The refusal, in the shape the run produced: the whole shown command, copied
    /// verbatim.</summary>
    [Fact]
    public void Citation_AShortenedCommandEndingInAQuote_Resolves()
    {
        var line = EvidenceLine();

        Resolve(ShownCommand(), line).Satisfied.Should().BeTrue(
            "the account cited the whole of what it was shown, and the command's own closing "
            + "quote is not the grammar's");
    }

    /// <summary>The same command cited the way p0474's re-ask teaches — with the closing
    /// apostrophe of the evidence line's own grammar attached.</summary>
    [Fact]
    public void Citation_AShortenedCommandEndingInAQuote_WithTheGrammarsClosingQuote_Resolves()
    {
        var line = EvidenceLine();

        Resolve(ShownCommand() + "'", line).Satisfied.Should().BeTrue();
    }

    /// <summary>A whole evidence line copied across goes through the quoted-span reading,
    /// which must trim the same way.</summary>
    [Fact]
    public void Citation_AWholeEvidenceLineOfACommandEndingInAQuote_Resolves()
    {
        var line = EvidenceLine();

        Resolve(line, line).Satisfied.Should().BeTrue();
    }

    /// <summary>p0481's guarantee is not spent to buy this one: the visible head of a
    /// shortened command still names its siblings rather than itself.</summary>
    [Fact]
    public void Citation_OnlyTheHeadOfACommandEndingInAQuote_DoesNotResolve()
    {
        var line = EvidenceLine();
        var head = ShownCommand().Split('…')[0];

        Resolve(head, line).Satisfied.Should().BeFalse(
            "a head does not tell two sibling commands apart, quote or no quote");
    }

    /// <summary>Trimming both sides must not make a command nobody ran resolvable.</summary>
    [Fact]
    public void Citation_ADifferentCommandEndingInAQuote_StillDoesNotResolve()
    {
        Resolve("dotnet test /work/Other.Server/Other.Server.sln --logger \"console;verbosity=minimal\"",
            EvidenceLine()).Satisfied.Should().BeFalse();
    }
}
