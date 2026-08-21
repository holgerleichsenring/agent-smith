using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0494: a run's third phase was refused on "final restore, build, and existing test
/// commands exit successfully for both affected repositories" over a command it had cited
/// PERFECTLY — the citation was rebuilt offline from the recorded command and came out byte
/// identical, 186 characters, elision marker included.
/// <para>
/// Two p0481 rules collided one character wide: the citation was trimmed of a trailing quote
/// that belonged to the COMMAND (<c>--logger "console;verbosity=minimal"</c>) rather than to
/// the evidence grammar, and a shortened command must be cited at exactly its own length.
/// The citation is now read as WRITTEN before it is read as trimmed, so a verbatim copy is
/// never shortened before it is compared.
/// </para>
/// </summary>
public sealed class VerbatimCitationTests
{
    /// <summary>The live verification script, with the repository names anonymised. Run
    /// through the log rather than frozen as a literal, so the fixture is the real elision
    /// boundary and not a hand-written string.</summary>
    private const string Verify =
        "set -e\n"
        + "dotnet restore Sample.Server.sln\n"
        + "dotnet build Sample.Server.sln --configuration Release --no-restore\n"
        + "dotnet test Sample.Server.sln --configuration Release --no-build "
        + "--logger \"console;verbosity=minimal\"";

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

    /// <summary>The refusal itself: the whole shown command, copied character for character.
    /// </summary>
    [Fact]
    public void Citation_VerbatimCopyOfAShortenedCommandEndingInAQuote_Resolves()
    {
        var line = EvidenceLine();
        var shown = ShownCommand();

        shown.Length.Should().Be(186, "the refused citation was 186 characters");
        shown.Should().EndWith("\"", "the last character is the command's own, not the grammar's");
        Resolve(shown, line).Satisfied.Should().BeTrue(
            "a verbatim copy of what the line showed names the command that ran");
    }

    /// <summary>p0481's own case: the account closes the unbalanced quote the elision left,
    /// citing one character more than the line showed. The trimmed reading still carries it.
    /// </summary>
    [Fact]
    public void Citation_TrailingGrammarQuoteAddedByTheAccount_StillResolves()
    {
        var line = EvidenceLine();

        Resolve(ShownCommand() + "'", line).Satisfied.Should().BeTrue();
    }

    /// <summary>The guarantee p0481 bought and this phase does not spend: two commands of one
    /// family share their head, so a head names neither of them in particular.</summary>
    [Fact]
    public void Citation_ShortenedCommandCitedByItsHeadOnly_StillRefused()
    {
        var line = EvidenceLine();
        var head = ShownCommand().Split('…')[0];

        Resolve(head, line).Satisfied.Should().BeFalse();
    }

    /// <summary>Adding a reading must not let a citation resolve against a command nobody
    /// ran.</summary>
    [Fact]
    public void Citation_CommandThatNeverRan_StillRefused()
    {
        Resolve("dotnet test Other.Server.sln --logger \"console;verbosity=minimal\"", EvidenceLine())
            .Satisfied.Should().BeFalse();
    }

    /// <summary>A whole evidence line copied across still resolves through its quoted span.
    /// </summary>
    [Fact]
    public void Citation_QuotedSpanReading_IsUnchanged()
    {
        var line = EvidenceLine();

        Resolve(line, line).Satisfied.Should().BeTrue();
        EvidenceCommand.Quoted(line).Should().NotBeEmpty("the line quotes the command it reports");
    }

    /// <summary>The trimmed reading keeps doing its own job — it is added to, not replaced.
    /// </summary>
    [Fact]
    public void EvidenceCommand_InCitation_StillTrimsTheGrammarsQuote()
    {
        EvidenceCommand.InCitation("dotnet build Sample.Server.sln'")
            .Should().Be("dotnet build Sample.Server.sln");
    }
}
