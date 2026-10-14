using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.AttackReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: what of an attack reviewer's answer stands — a cut quoting its own register on a read
/// of its own, a finding on a read or a proof of absence of its own, and a verdict no more severe than that.
/// </summary>
public sealed class SpecAttackAdmissionTests
{
    private const string Handler = "src/Export/ExportHandler.cs";

    [Fact]
    public void SpecAttackAdmission_CutQuotingItsRegister_WithOwnRead_IsKept()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 40);

        var result = Admit(Answer("BUILD WITH CUTS", Cut("done", "lands in the export file", Handler, 12, id)), look);

        result.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        result.Cuts.Should().ContainSingle().Which.Should().Be(
            new SpecReviewCut("done", "lands in the export file", "lands in two files", $"shop/{Handler}:12 [{id}]"));
    }

    [Fact]
    public void SpecAttackAdmission_CutQuotingAnotherRegister_IsDiscarded()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 40);

        var result = Admit(Answer("BUILD WITH CUTS", Cut("goal", "lands in the export file", Handler, 12, id)), look);

        result.Cuts.Should().BeEmpty("the quote is the done entry's, not the goal's");
        result.Verdict.Should().Be(SpecReviewVerdicts.Build);
    }

    [Fact]
    public void SpecAttackAdmission_EvidenceCitingNoReadOfItsOwn_IsDiscarded()
    {
        var look = Look();
        Read(look, Handler, lines: 40);

        var result = Admit(Answer("REFUSE", findings: Finding("the store is never read", Handler, 12, "A9")), look);

        result.Findings.Should().BeEmpty("A9 was never minted");
    }

    [Fact]
    public void SpecAttackAdmission_LinePastWhatTheReadReturned_IsDiscarded()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 10);

        var result = Admit(Answer("REFUSE", Cut("done", "lands in the export file", Handler, 12, id),
            Finding("line twelve is wrong", Handler, 12, id)), look);

        (result.Cuts.Count, result.Findings.Count).Should().Be((0, 0), "the read returned ten lines");
    }

    [Fact]
    public void SpecAttackAdmission_RefuseWithNoAdmittedFinding_IsStoredAsWhatItSupports()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 40);

        var result = Admit(Answer("REFUSE", Cut("done", "lands in the export file", Handler, 12, id),
            Finding("an invented path", "src/Nowhere.cs", 3, id)), look);

        result.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts, "only the cut was admitted");
    }

    [Fact]
    public void SpecAttackAdmission_BuildCarryingAnAdmittedCut_IsBuildWithCuts()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 40);

        var result = Admit(Answer("BUILD", Cut("scope", "The import side stays", Handler, 2, id)), look);

        result.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts, "a named cut is a cut");
    }

    [Fact]
    public void SpecAttackAdmission_FindingCitingItsOwnExitOneSearch_IsKept()
    {
        var look = Look();
        var id = Search(look, "OrderStore", exit: 1);

        var result = Admit(Answer("REFUSE", findings: Finding("nothing reads the order store", null, 0, id)), look);

        result.Verdict.Should().Be(SpecReviewVerdicts.Refuse);
        result.Findings.Should().ContainSingle().Which.Evidence.Should().Be($"shop search:'OrderStore' [{id}]");
    }

    [Fact]
    public void SpecAttackAdmission_FindingCitingAReadOfAMissingFile_IsKept()
    {
        var look = Look();
        var id = Read(look, Handler, lines: 0, exit: 1);

        var result = Admit(Answer("REFUSE", findings: Finding("the cited handler does not exist", Handler, 0, id)), look);

        result.Verdict.Should().Be(SpecReviewVerdicts.Refuse);
        result.Findings.Single().Evidence.Should().Be($"shop/{Handler} [{id}]");
    }

    [Fact]
    public void SpecAttackAdmission_CutCitingOnlyASearch_IsDiscarded()
    {
        var look = Look();
        var id = Search(look, "OrderStore", exit: 0);

        var result = Admit(Answer("BUILD WITH CUTS", Cut("done", "lands in the export file", Handler, 12, id)), look);

        result.Cuts.Should().BeEmpty("a cut changes text about a line, and a search read no line");
    }

    [Fact]
    public void SpecAttackAdmission_EvidenceNamesTheRepositoryNotTheSandboxKey()
    {
        var look = Look();
        var id = Read(look, "./" + Handler, lines: 40);

        var result = Admit(Answer("REFUSE", findings: Finding("the store is read twice", Handler, 5, id)), look);

        result.Findings.Single().Evidence.Should().Be($"shop/{Handler}:5 [{id}]").And.NotContain(Key);
    }

    private static string Answer(string verdict, string? cuts = null, string? findings = null) =>
        $$"""Here is my answer, citing [A1]: {"verdict": "{{verdict}}", "cuts": [{{cuts}}], "findings": [{{findings}}]}""";

    private static string Cut(string register, string quote, string path, int line, string cites) =>
        $$"""{"register": "{{register}}", "quote": "{{quote}}", "replacement": "lands in two files", "repository": "{{Key}}", "path": "{{path}}", "line": {{line}}, "cites": "{{cites}}"}""";

    private static string Finding(string text, string? path, int line, string cites) =>
        $$"""{"text": "{{text}}", "repository": "{{Key}}", "path": {{(path is null ? "null" : $"\"{path}\"")}}, "line": {{line}}, "cites": "{{cites}}"}""";
}
