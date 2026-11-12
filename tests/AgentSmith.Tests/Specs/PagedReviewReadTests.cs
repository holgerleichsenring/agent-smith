using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-12-cd4cl: a reviewer reads on past the per-call cut — read_file takes start_line, numbers stay the file's
/// own, the cut names the line to continue at, and the evidence id admits only the lines that page showed.
/// </summary>
public sealed class PagedReviewReadTests
{
    [Fact]
    public void ReadPage_From_StartsAtTheLineAndIsEmptyPastTheEnd()
    {
        ReadPage.From("a\nb\nc", 1).Should().Be("a\nb\nc");
        ReadPage.From("a\nb\nc", 2).Should().Be("b\nc");
        ReadPage.From("a\nb\nc", 9).Should().BeEmpty();
    }

    [Fact]
    public void ReadPage_Bound_NamesTheFirstLineNotCarriedWhole()
    {
        var numbered = RepositoryFileReadTool.Numbered(string.Join("\n", Enumerable.Range(1, 10).Select(_ => "xxxx")), 5);

        numbered.Should().StartWith("5\txxxx");
        ReadPage.Bound(numbered, 5, 18).Should().EndWith("read on with start_line=7]",
            "lines 5 and 6 fit whole in 18 characters, line 7 is cut");
        ReadPage.Bound(numbered, 5, 10_000).Should().Be(numbered);
    }

    [Fact]
    public async Task RepositoryFileReadTool_StartLine_ReturnsAbsoluteNumbersAndTheContinueLine()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/src/Big.cs"] = string.Join("\n", Enumerable.Range(1, 3000).Select(i => $"line {i} " + new string('y', 20)));
        var look = new DerivationLook(new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) },
            new FixedReaderFactory(files), new PackageEcosystemDetector(), NullLogger.Instance,
            templates: null, DerivationLookTerms.SpecReview);
        var tool = new RepositoryFileReadTool(look, new FixedReaderFactory(files), NullLogger.Instance);

        var first = await tool.ReadFile(Repo, "src/Big.cs");
        var next = int.Parse(first[(first.LastIndexOf("start_line=", StringComparison.Ordinal) + 11)..^1]);
        var second = await tool.ReadFile(Repo, "src/Big.cs", start_line: next);

        second.Should().Contain($"\n{next}\tline {next} ");
        var paged = look.Evidence.Looks[1];
        paged.FirstLine.Should().Be(next);
        OwnReadRule.Within(next, paged).Should().BeTrue();
        OwnReadRule.Within(next - 1, paged).Should().BeFalse("that line came from the first page, under its own id");
        OwnReadRule.Within(next - 1, look.Evidence.Looks[0]).Should().BeTrue();
    }

    [Fact]
    public void OwnReadRule_PagedRead_AdmitsOnlyTheShownLines()
    {
        var read = new EvidenceLook("P2", Repo, "read a", 0, true, "[P2]", EvidenceRecord.Read, "a", LinesReturned: 10, FirstLine: 41);

        OwnReadRule.Within(41, read).Should().BeTrue();
        OwnReadRule.Within(50, read).Should().BeTrue();
        OwnReadRule.Within(51, read).Should().BeFalse();
        OwnReadRule.Within(40, read).Should().BeFalse();
    }

    [Fact]
    public void ProposalReviewLook_AllowanceFromAgentConfig()
    {
        DerivationLookTerms.ProposalReviewWith(new AgentConfig().DialogReviewLookAllowance).Allowance.Should().Be(24);
        var terms = DerivationLookTerms.ProposalReviewWith(40);
        (terms.Allowance, terms.Actor, terms.EvidencePrefix).Should().Be((40, "proposal review", "P"));
    }
}
