using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: what a look remembers ABOUT ITSELF. The minted line is prose for the
/// model; the record beside it is what an admission rule reads, and a rule that has to decide
/// whether a finding rests on a read of one path cannot read that off a sentence.
/// </summary>
public sealed class PhaseReviewEvidenceTests
{
    [Fact]
    public void Evidence_EveryCaller_RecordsKindPathAndExit()
    {
        var evidence = new DerivationEvidence("P", "the phase review");

        evidence.Remember(new EvidenceRecord("api", EvidenceRecord.Read, "read src/A.cs", 0, true, "src/A.cs", 40));
        evidence.Remember(new EvidenceRecord("api", EvidenceRecord.Search, "grep -E 'x' .", 1, true, "."));
        evidence.Remember(new EvidenceRecord("api", EvidenceRecord.Audit, "dotnet list package", 0, true));
        evidence.RememberOnce(new EvidenceRecord("template:web", EvidenceRecord.TemplateProof, "read context.yaml", 0, true, "context.yaml"));

        evidence.Looks.Select(l => l.Kind).Should().Equal(
            EvidenceRecord.Read, EvidenceRecord.Search, EvidenceRecord.Audit, EvidenceRecord.TemplateProof);
        evidence.Looks[0].Path.Should().Be("src/A.cs");
        evidence.Looks[0].LinesReturned.Should().Be(40);
        evidence.Looks[2].Path.Should().BeNull("an audit is about a repository, not a path");
        evidence.Looks.Select(l => l.ExitCode).Should().Equal(0, 1, 0, 0);
    }

    [Fact]
    public void Evidence_TheMintedLine_IsUnchanged()
    {
        var evidence = new DerivationEvidence("P", "the phase review");

        evidence.Remember(new EvidenceRecord("api", EvidenceRecord.Read, "read src/A.cs", 0, true, "src/A.cs", 40));

        evidence.Lines.Should().ContainSingle().Which
            .Should().Be("[P1] api: the phase review ran 'read src/A.cs' exited 0");
    }

    [Fact]
    public void PhaseReviewRead_IsNumberedAndDerivationReadIsNot()
    {
        DerivationLookTerms.PhaseReview.NumberedReads.Should().BeTrue(
            "a finding names a LINE, and an unnumbered read gives the model nothing to name");
        DerivationLookTerms.Derivation.NumberedReads.Should().BeFalse();
        DerivationLookTerms.CutReview.NumberedReads.Should().BeFalse(
            "the cut review quotes text; numbers would be tokens it never spends");

        RepositoryFileReadTool.Numbered("alpha\nbeta").Should().Be("1\talpha\n2\tbeta");
    }

    [Fact]
    public void PhaseReviewRead_ATrailingNewline_DoesNotInventALine()
    {
        RepositoryFileReadTool.Numbered("alpha\nbeta\n").Should().Be("1\talpha\n2\tbeta",
            "the trailing newline TERMINATES the last line; it does not open another");
        RepositoryFileReadTool.LinesWithin(
            RepositoryFileReadTool.Numbered("alpha\nbeta\n"), 10_000).Should().Be(2,
            "a phantom third line would admit a finding on a line the file does not have");
    }

    [Fact]
    public void PhaseReviewRead_LinesReturned_StopAtTheBound()
    {
        var numbered = RepositoryFileReadTool.Numbered(string.Join("\n", Enumerable.Range(1, 50).Select(i => new string('x', 20))));

        RepositoryFileReadTool.LinesWithin(numbered, numbered.Length).Should().Be(50);
        RepositoryFileReadTool.LinesWithin(numbered, 100).Should().BeLessThan(50,
            "a line the bound cut in half is not a line the reviewer saw");
    }

    [Fact]
    public void PhaseReview_Terms_AreItsOwn()
    {
        DerivationLookTerms.PhaseReview.Actor.Should().Be("phase review");
        DerivationLookTerms.PhaseReview.Allowance.Should().Be(8);
        DerivationLookTerms.PhaseReview.EvidencePrefix.Should().Be("P");
    }
}
