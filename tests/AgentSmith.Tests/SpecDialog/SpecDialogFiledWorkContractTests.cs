using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-deda: a design turn in a conversation that has filed work is told, in the
/// per-turn framework contract, that it cannot change what was filed and must never report
/// having done so. The trigger is the conversation's filing record — a kept filing turn —
/// so the clause reaches every turn kind and names no ticket.
/// </summary>
public sealed class SpecDialogFiledWorkContractTests
{
    private const string Clause = "You cannot change, close or re-cut";

    private static string Prompt(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return new SpecDialogPromptFactory().Build(pipeline, 0, 0);
    }

    // The same conversation twice, differing only in the kind of its last assistant turn:
    // neither kind is an answer, so the proposal contract is identical and the clause is the
    // only difference between the two prompts.
    private static SpecDialogTurn[] EndingIn(SpecDialogTurnKind kind) =>
    [
        new("user", "update the three libraries"),
        new("assistant", "found three of them", SpecDialogTurnKind.Answer),
        new("user", "all three, one phase"),
        new("assistant", "Filed WIDGET-1, WIDGET-2, WIDGET-3", kind),
    ];

    [Fact]
    public void TurnContract_AConversationThatFiled_CarriesTheClause()
    {
        var prompt = Prompt(EndingIn(SpecDialogTurnKind.Filing));

        prompt.Should().Contain("This conversation has already filed work")
            .And.Contain(Clause)
            .And.Contain("never report having done so");
    }

    [Fact]
    public void TurnContract_AConversationThatNeverFiled_IsUnchanged()
    {
        var unfiled = Prompt(EndingIn(SpecDialogTurnKind.Notice));
        var filed = Prompt(EndingIn(SpecDialogTurnKind.Filing));

        unfiled.Should().NotContain("already filed work").And.NotContain(Clause)
            .And.EndWith("otherwise reply with no artifact.");
        filed.Should().StartWith(unfiled, "the clause is added to the contract, not woven into it")
            .And.NotBe(unfiled);
    }

    [Fact]
    public void TurnContract_AFiledConversation_CarriesTheClauseOnAProposalTurn()
    {
        var prompt = Prompt(EndingIn(SpecDialogTurnKind.Filing));

        prompt.Should().Contain("MAY propose").And.NotContain("MAY NOT propose");
        prompt.Should().Contain(Clause, "a proposal turn files nothing either");
    }

    [Fact]
    public void TurnContract_AFiledConversation_CarriesTheClauseOnAnAnswerTurn()
    {
        var prompt = Prompt(
            new SpecDialogTurn("user", "file the three of them"),
            new SpecDialogTurn("assistant", "Filed WIDGET-1, WIDGET-2, WIDGET-3",
                SpecDialogTurnKind.Filing),
            new SpecDialogTurn("user", "now combine them into one"));

        prompt.Should().Contain("MAY NOT propose", "no discussion has been replied to since");
        prompt.Should().Contain(Clause, "the clause does not key on what the turn may produce");
    }

    [Fact]
    public void TurnContract_TheClause_NamesNoTicketAndAssertsNoTrackerState()
    {
        var unfiled = Prompt(EndingIn(SpecDialogTurnKind.Notice));
        var clause = Prompt(EndingIn(SpecDialogTurnKind.Filing))[unfiled.Length..];

        clause.Should().NotBeEmpty();
        clause.Should().NotContain("WIDGET-", "the transcript already names what was filed");
        clause.Should().NotContain("stands", "a one-column snapshot cannot assert present tracker state");
        clause.Should().NotContain("ticket", "naming the unit invites naming the tickets");
    }
}
