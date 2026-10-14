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
