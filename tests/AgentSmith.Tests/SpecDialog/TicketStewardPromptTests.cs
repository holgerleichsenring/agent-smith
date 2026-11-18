using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-da5ef: the ticket steward's prompt contract — the conversation, the ticket and the framework's facts
/// for it, and a reply that answers and acts but never proposes; and its reply reaches the page as written.
/// </summary>
public sealed class TicketStewardPromptTests
{
    [Fact]
    public void Build_BoundTurn_CarriesTranscriptFactsAndTheAnswerContract()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "go on", Author: "Ada")]);
        pipeline.Set(ContextKeys.SpecDialogFrameworkFacts,
            new FrameworkFacts("agentsmith/42", "enqueued", "in-progress", "approved"));

        var prompt = new TicketStewardPromptFactory().Build(pipeline, imagesExisting: 0, imagesCarried: 0);

        prompt.Should().StartWith("## Ticket conversation").And.Contain("[user: Ada] go on")
            .And.Contain("agentsmith/42").And.Contain("This ticket has already run").And.Contain("Propose nothing");
    }

    // 2026-11-18-d6d2b: the steward keeps what a tool showed apart from what it infers, and asks instead of guessing.
    [Fact]
    public void Build_Contract_SeparatesCheckedFromInferred_AndAsks()
    {
        var prompt = new TicketStewardPromptFactory().Build(Bound(failedStatus: "Failed"), 0, 0);

        prompt.Should().Contain("name the tool each fact came").And.Contain("Say an inference as one")
            .And.Contain("never report a finding, verdict or reason that no tool showed you")
            .And.Contain("ask the person in your reply instead of guessing");
    }

    [Theory]
    [InlineData("Failed", "moves this ticket to `Failed`")]
    [InlineData(null, "leaves this ticket in its tracker status and the claim door holds it")]
    public void Build_StewardSaysWhereAFailureLeavesTheTicket(string? failedStatus, string expected) =>
        new TicketStewardPromptFactory().Build(Bound(failedStatus), 0, 0).Should().Contain("## How this ticket starts again")
            .And.Contain(expected).And.Contain("does not start it again by itself");

    [Fact]
    public void Build_StartArmSeeded_SaysAReplyStartsTheNextAttemptThroughTheSeries()
    {
        var pipeline = Bound("Failed");
        pipeline.Set(ContextKeys.SpecDialogAttemptStart, Moq.Mock.Of<AgentSmith.Contracts.Dialogue.IAttemptStart>());

        new TicketStewardPromptFactory().Build(pipeline, 0, 0).Should().Contain("start it with start_next_attempt")
            .And.Contain("edit_series first");
        new TicketStewardPromptFactory().Build(Bound("Failed"), 0, 0).Should().Contain("This person may not start runs");
    }

    [Fact]
    public void DesignPartnerFacts_CarryNoRestartSentences() =>
        FrameworkFactsSection.Render(Bound("Failed")).Should().NotContain("starts again").And.NotContain("`Failed`");

    private static PipelineContext Bound(string? failedStatus)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "cover every case", Author: "Ada")]);
        pipeline.Set(ContextKeys.SpecDialogFrameworkFacts,
            new FrameworkFacts("agentsmith/42", "enqueued", "in-progress", "approved", failedStatus));
        return pipeline;
    }

    [Fact]
    public void Build_EmptyTranscript_Throws()
    {
        var build = () => new TicketStewardPromptFactory().Build(new PipelineContext(), 0, 0);

        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TurnResult_StewardReplyOnTheDashboard_KeepsItsQuotedYaml()
    {
        const string reply = "It ran:\n\n```yaml\nspec: p9001\n```";

        SpecDialogTurnResult.On("dashboard", reply, new AnswerOutcome(), stewarded: true).Shown.Should().Be(reply);
        SpecDialogTurnResult.On("dashboard", reply, new AnswerOutcome()).Shown.Should().NotContain("```yaml",
            "the design partner's draft is shown by the proposal pane");
    }
}
