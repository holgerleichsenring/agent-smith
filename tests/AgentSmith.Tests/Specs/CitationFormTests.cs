using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0473: the account has to be able to write a citation its own resolver accepts.
/// <para>
/// p0469 required a cited command to be a substantial prefix of one that ran, and left the
/// prompt saying only "cite the command". A live run then saw the evidence, ran an
/// inventory over both repositories, got a PASS from each, and was refused for describing
/// the two commands in prose. These pin the form that must work and the forms that must
/// keep failing.
/// </para>
/// </summary>
public sealed class CitationFormTests
{
    // The shape PhaseCommandLog writes. The command carries its own apostrophes, which is
    // what defeated first-to-last-apostrophe extraction.
    private const string Heredoc = "python3 - <<'PY'\nimport sys\nprint('PASS')\nPY";
    private static readonly string ServerRan =
        $"Sample.Server: the agent ran '{Heredoc}' exited 0 — output: PASS: no owned references found";
    private static readonly string WorkerRan =
        $"Sample.Worker: the agent ran '{Heredoc}' exited 0 — output: PASS: no owned references found";

    [Fact]
    public void CitationResolver_TheLiveRegression_VerbatimFormOfThatCitation_Resolves()
    {
        var account = Resolve(Heredoc, ServerRan, WorkerRan);

        account.IsSatisfied.Should().BeTrue(
            "the account copied the command verbatim, which is the form the prompt asks for");
    }

    [Fact]
    public void CitationResolver_CommandContainingQuotes_ResolvesFromItsOwnDelimiters()
    {
        // Taking the last apostrophe in the line would end the command inside <<'PY'.
        EvidenceCommandFor(ServerRan).Should().Be(Heredoc);
    }

    [Fact]
    public void CitationResolver_ProseDescriptionOfTwoCommands_DoesNotResolve()
    {
        var prose = "python3 assertion inventory in Sample.Server and Sample.Worker "
            + "(both exited 0: \"PASS: no owned references found\")";

        Resolve(prose, ServerRan, WorkerRan).IsSatisfied.Should().BeFalse(
            "a description names no command, however accurately it reports what they did");
    }

    // p0474 retired the separator this test pinned. A semicolon is a shell operator and
    // twenty of one live run's commands carried one, so splitting on it shattered correctly
    // quoted commands. Two commands are two ELEMENTS now.
    [Fact]
    public void CitationResolver_TwoCommandsAsTwoElements_BothResolve()
    {
        var account = new CitationResolver(
                CitedFileIndex.FromDiff(string.Empty),
                [ServerRan, "Sample.Server: build 'dotnet build Sample.sln' exited 0"])
            .Resolve(new AccountRow(
                "criterion", AccountDisposition.Satisfied, null, "note", [Heredoc, "dotnet build Sample.sln"]));

        account.IsSatisfied.Should().BeTrue("every element names a command that ran");
    }

    [Fact]
    public void CitationResolver_CitationTakenFromCommandOutput_StillDoesNotResolve()
    {
        Resolve("PASS: no owned references found", ServerRan).IsSatisfied.Should().BeFalse(
            "p0469 closed this and this phase does not reopen it");
    }

    [Fact]
    public void SpecAccountPrompt_CitationForm_TellsTheAccountToQuoteVerbatimAndSplitOnSemicolons()
    {
        var prompt = SpecAccountPrompt.For(["criterion"], string.Empty, []);

        prompt.Should().Contain("VERBATIM", "the resolver accepts nothing else")
            .And.Contain("semicolon", "the multi-command form exists and must be stated");
    }

    [Fact]
    public void CitationResolver_QuotedSpanRunningAcrossTwoParts_DoesNotResolve()
    {
        // Reading the quoted span from the first apostrophe to the last one yields
        // "dotnet build' exited 0; worker: build 'never ran", which BEGINS with a command
        // that ran. While the prefix test was symmetric that was enough to close a
        // criterion on a command nobody executed.
        var mixed = "api: build 'dotnet build' exited 0; worker: build 'never ran' exited 0";

        Resolve(mixed, "api: build 'dotnet build' exited 0").IsSatisfied.Should().BeFalse(
            "a citation that merely starts with a real command names the invented one too");
    }

    private static CriterionAccount Resolve(string citation, params string[] commands) =>
        new CitationResolver(CitedFileIndex.FromDiff(string.Empty), commands)
            .Resolve(new AccountRow("criterion", AccountDisposition.Satisfied, citation, "note"));

    private static string EvidenceCommandFor(string line) => EvidenceCommand.InEvidence(line);
}
