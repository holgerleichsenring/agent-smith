using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.PremiseCheckTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-17-c47ea: a false premise in an APPROVED spec is a corrected fact the master builds on, recorded in the run's
/// decision log; only one that breaks a done criterion — or any in a spec nobody approved — is handed back.
/// </summary>
public sealed class PremiseCorrectionTests
{
    private const string Criterion = "the dispatch no longer runs inside the handler";

    private static string Falsified(string? breaks) =>
        $$"""
        [{"premise": "OrderHandler validates the payload before dispatch",
          "verdict": "no-longer-holds", "why": "only one path validates; the other cascades",
          "cites": "M1", "breaks": {{(breaks is null ? "null" : $"\"{breaks}\"")}}}]
        """;

    private static Harness Approved(string? breaks)
    {
        var h = For(Draft(), Falsified(breaks), searches: (DerivationTestLooks.Repo, "Validate"));
        h.Pipeline.Set(ContextKeys.SpecSet, h.Pipeline.Get<SpecSet>(ContextKeys.SpecSet) with
        {
            Approval = new SpecApproval(DateTimeOffset.UtcNow, "conv-1", "sample.user"),
        });
        return h;
    }

    [Fact]
    public async Task PremiseCheck_ApprovedSpecFalsePremise_IsACorrectionAndTheSpecRuns()
    {
        var h = Approved(breaks: null);

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("an approved spec is built on the corrected fact");
        h.Verdict.Should().BeNull("nothing is handed back");
        h.Ticket.Comments.Should().BeEmpty();
        h.Decisions.Decisions.Should().ContainSingle().Which
            .Should().Contain("OrderHandler validates the payload before dispatch")
            .And.Contain("only one path validates; the other cascades")
            .And.Contain("[M1]");
        h.Pipeline.Get<IReadOnlyList<string>>(ContextKeys.PremiseCorrections).Should().ContainSingle()
            .Which.Should().Contain("the spec claims").And.Contain("[M1]");
        h.Pipeline.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs.Single(p => p.SpecId == SpecId)
            .Note.Should().Contain("Premise corrected in " + SpecId);
    }

    [Fact]
    public async Task PremiseCheck_ApprovedSpecBrokenCriterion_IsHandedBackAndNothingCorrected()
    {
        var h = Approved(breaks: Criterion);

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse();
        h.Verdict.Should().Contain("False premise in " + SpecId);
        h.Ticket.Comments.Should().ContainSingle().Which.Should().Contain(PremiseHandback.Heading);
        h.Pipeline.Get<IReadOnlyList<string>>(ContextKeys.PremiseCorrections).Should().BeEmpty();
    }

    [Fact]
    public async Task PremiseCheck_ApprovedSpecInventedCriterion_BreaksNothing()
    {
        var h = Approved(breaks: "a criterion this spec never states at all");

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("a criterion the spec does not state cannot be broken");
        h.Verdict.Should().BeNull();
    }

    [Fact]
    public async Task PremiseCheck_UnapprovedSpecFalsePremise_IsHandedBackAsBefore()
    {
        var h = For(Draft(), Falsified(null), searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse("nobody approved a derived spec's facts");
        h.Verdict.Should().NotBeNull();
        h.Pipeline.Get<IReadOnlyList<string>>(ContextKeys.PremiseCorrections).Should().BeEmpty();
    }

    [Fact]
    public void PremiseCorrections_Block_RendersOnlyWhenSet()
    {
        var pipeline = new PipelineContext();
        PremiseCorrection.PromptBlock(pipeline).Should().BeEmpty();
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.PremiseCorrections, []);
        PremiseCorrection.PromptBlock(pipeline).Should().BeEmpty();

        pipeline.Set<IReadOnlyList<string>>(ContextKeys.PremiseCorrections, [PremiseCorrection.Line(
            new PremiseFinding("a fact", "no-longer-holds", "it is otherwise", "M1", "[M1] repo: read x exited 0", "repo: read x"))]);

        PremiseCorrection.PromptBlock(pipeline).Should().Contain("Corrected premises")
            .And.Contain("\"a fact\"").And.Contain("it is otherwise").And.Contain("[M1]").And.Contain("revise_spec").And.Contain("ask_human");
    }
}
