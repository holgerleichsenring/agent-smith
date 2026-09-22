using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// p0315e: the outcome protocol — plain prose is an answer, a bare yaml block
/// is a phase (unchanged p0315b contract), one ```outcome block carries bug or
/// epic, and every malformed shape fails with an error naming exactly what.
/// </summary>
public sealed class OutcomeProposalResolverTests
{
    private readonly OutcomeProposalResolver _resolver = BuildResolver();

    private static OutcomeProposalResolver BuildResolver()
    {
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var reader = new PhaseDraftReader();
        return new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker()));
    }

    [Fact]
    public void Resolve_PlainProse_ResolvesAnswer()
    {
        var resolution = _resolver.Resolve("Dispatch goes through the intent engine.");

        resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public void Resolve_BareYamlDraft_ResolvesPhaseWithExtractedFields()
    {
        var resolution = _resolver.Resolve(
            """
            Draft:
            ```yaml
            phase: p9999
            goal: "Add the widget endpoint"
            requires: [p0100]
            ```
            """);

        var phase = resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<PhaseOutcome>().Subject;
        phase.Draft.PhaseId.Should().Be("p9999");
        phase.Draft.Goal.Should().Be("Add the widget endpoint");
        phase.Draft.Requires.Should().Equal("p0100");
    }

    [Fact]
    public void Resolve_BugBlock_ResolvesBugTicketShape()
    {
        var resolution = _resolver.Resolve(
            """
            ```outcome
            kind: bug
            title: "Fix the null deref"
            description: "AppendTurnAsync dereferences a null session."
            acceptance_criteria: "No NRE on a thread without a session."
            ```
            """);

        var bug = resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<BugOutcome>().Subject;
        bug.Ticket.Title.Should().Be("Fix the null deref");
        bug.Ticket.AcceptanceCriteria.Should().Be("No NRE on a thread without a session.");
    }

    [Fact]
    public void Resolve_BugMissingTitle_InvalidNamesField()
    {
        var resolution = _resolver.Resolve(
            "```outcome\nkind: bug\ndescription: \"something broke\"\n```");

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("title");
    }

    [Fact]
    public void Resolve_UnknownKind_Invalid()
    {
        var resolution = _resolver.Resolve("```outcome\nkind: refactor\n```");

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("refactor");
    }

    [Fact]
    public void Resolve_TwoOutcomeBlocks_Invalid()
    {
        var resolution = _resolver.Resolve(
            "```outcome\nkind: bug\n```\n```outcome\nkind: bug\n```");

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("exactly one");
    }

    [Fact]
    public void Resolve_OutcomeBlockPlusYamlBlock_Invalid()
    {
        var resolution = _resolver.Resolve(
            """
            ```outcome
            kind: bug
            title: "t"
            description: "d"
            ```
            ```yaml
            phase: p9999
            goal: "g"
            ```
            """);

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("mixes");
    }

    [Fact]
    public void Resolve_EpicValid_ResolvesOrderedChildrenWithEdges()
    {
        var resolution = _resolver.Resolve(EpicReply(
            child2Requires: "requires: [p9000a]"));

        var epic = resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<EpicOutcome>().Subject;
        epic.Parent.PhaseId.Should().Be("p9000");
        epic.Children.Select(c => c.PhaseId).Should().Equal("p9000a", "p9000b");
        epic.Children[1].Requires.Should().Equal("p9000a");
    }

    /// <summary>
    /// 2026-09-22-b3d7: the FILING SHAPE changed — one ticket, no slice records — and the `epic`
    /// KIND did not. Collapsing it into the phase outcome would have deleted checks, not tickets:
    /// a phase outcome carries ONE draft, so a set of two or more has no representation on that
    /// path at all, and the at-least-two rule, the phase cap, the per-child done list and the
    /// sibling and cycle edge checks live only here. The parent draft is the work ticket's own
    /// requirement text and has nowhere else to live.
    /// </summary>
    [Fact]
    public void EpicOutcomeParser_ACutOfTwoOrMoreSlices_StillResolvesToAnEpicOutcome()
    {
        var resolution = _resolver.Resolve(EpicReply());

        var epic = resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<EpicOutcome>(
                "a cut is how the model says several slices, whatever the filing costs").Subject;
        epic.Parent.Goal.Should().Be("Widget platform end to end");
        epic.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Resolve_EpicFreeTextPrecondition_Allowed()
    {
        var resolution = _resolver.Resolve(EpicReply(
            child2Requires: "requires: [\"the storage layer is deployed\"]"));

        resolution.Should().BeOfType<OutcomeResolved>();
    }

    [Fact]
    public void Resolve_EpicSingleChild_Invalid()
    {
        var resolution = _resolver.Resolve(
            """
            ```outcome
            kind: epic
            parent:
              phase: p9000
              goal: "g"
            children:
              - phase: p9000a
                goal: "only slice"
            ```
            """);

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("at least two");
    }

    [Fact]
    public void Resolve_EpicChildRequiresUnknownSibling_Invalid()
    {
        var resolution = _resolver.Resolve(EpicReply(child2Requires: "requires: [p7777]"));

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("p7777").And.Contain("sibling");
    }

    [Fact]
    public void Resolve_EpicChildRequiresParent_Invalid()
    {
        var resolution = _resolver.Resolve(EpicReply(child2Requires: "requires: [p9000]"));

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("parent");
    }

    [Fact]
    public void Resolve_EpicCycle_Invalid()
    {
        var resolution = _resolver.Resolve(EpicReply(
            child1Requires: "requires: [p9000b]", child2Requires: "requires: [p9000a]"));

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("cycle");
    }

    [Fact]
    public void Resolve_EpicChildFailsSchema_InvalidNamesChild()
    {
        var resolution = _resolver.Resolve(
            """
            ```outcome
            kind: epic
            parent:
              phase: p9000
              goal: "g"
            children:
              - phase: p9000a
                goal: "ok slice"
              - phase: not-a-phase-id
                goal: "broken slice"
            ```
            """);

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("child #2");
    }

    /// <summary>
    /// 2026-09-17-042eb: a child is worked weeks after it is filed, and its done list is what says
    /// when it is finished — the refusal names the rule so the retry knows what to add.
    /// </summary>
    [Fact]
    public void Resolve_EpicChildWithoutDone_InvalidNamingTheRule()
    {
        var resolution = _resolver.Resolve(EpicReply(child1Done: string.Empty));

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain("child #1").And.Contain("p9000a").And.Contain("'done:'");
    }

    private static string EpicReply(
        string? child1Requires = null, string? child2Requires = null,
        string child1Done = "done: [\"a widget is stored\"]") =>
        $"""
        ```outcome
        kind: epic
        parent:
          phase: p9000
          goal: "Widget platform end to end"
        children:
          - phase: p9000a
            goal: "Widget storage layer"
            {child1Requires}
            {child1Done}
          - phase: p9000b
            goal: "Widget API"
            {child2Requires}
            done: ["the API returns a stored widget"]
        ```
        """;
}
