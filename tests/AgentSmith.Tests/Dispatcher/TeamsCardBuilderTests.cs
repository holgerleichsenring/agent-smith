using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Services.Adapters;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class TeamsCardBuilderTests
{
    private static DialogQuestion CreateQuestion(
        QuestionType type,
        string questionId = "q-1",
        string text = "Do you approve?",
        string? context = null,
        IReadOnlyList<string>? choices = null)
    {
        return new DialogQuestion(
            questionId, type, text, context, choices, null, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Confirmation_ContainsYesAndNoActions()
    {
        var card = TeamsCardBuilder.BuildQuestionCard(CreateQuestion(QuestionType.Confirmation));
        var json = card.ToJsonString();

        json.Should().Contain("\"answer\":\"yes\"");
        json.Should().Contain("\"answer\":\"no\"");
        json.Should().Contain("Action.Submit");
        json.Should().Contain("Do you approve?");
    }

    [Fact]
    public void Choice_ContainsAllChoiceButtons()
    {
        var question = CreateQuestion(QuestionType.Choice,
            choices: new[] { "Option A", "Option B", "Option C" });

        var card = TeamsCardBuilder.BuildQuestionCard(question);
        var json = card.ToJsonString();

        json.Should().Contain("Option A");
        json.Should().Contain("Option B");
        json.Should().Contain("Option C");
        json.Should().Contain("Action.Submit");
    }

    [Fact]
    public void Approval_ContainsApproveRejectAndCommentInput()
    {
        var card = TeamsCardBuilder.BuildQuestionCard(CreateQuestion(QuestionType.Approval));
        var json = card.ToJsonString();

        json.Should().Contain("\"answer\":\"approve\"");
        json.Should().Contain("\"answer\":\"reject\"");
        json.Should().Contain("Input.Text");
        json.Should().Contain("comment");
    }

    [Fact]
    public void FreeText_ContainsInputFieldAndSubmit()
    {
        var card = TeamsCardBuilder.BuildQuestionCard(CreateQuestion(QuestionType.FreeText));
        var json = card.ToJsonString();

        json.Should().Contain("Input.Text");
        json.Should().Contain("freetext");
        json.Should().Contain("__freetext__");
        json.Should().Contain("Action.Submit");
    }

    [Fact]
    public void Info_ContainsAcknowledgeButton()
    {
        var card = TeamsCardBuilder.BuildQuestionCard(CreateQuestion(QuestionType.Info));
        var json = card.ToJsonString();

        json.Should().Contain("Acknowledge");
        json.Should().Contain("\"answer\":\"ack\"");
    }

    [Fact]
    public void Context_IncludedWhenProvided()
    {
        var question = CreateQuestion(QuestionType.Confirmation, context: "Extra context here");
        var card = TeamsCardBuilder.BuildQuestionCard(question);
        var json = card.ToJsonString();

        json.Should().Contain("Extra context here");
    }

    [Fact]
    public void ProgressCard_ContainsStepInfo()
    {
        var card = TeamsCardBuilder.BuildProgressCard(3, 10, "fix-bug");
        var json = card.ToJsonString();

        json.Should().Contain("[3/10]");
        json.Should().Contain("fix-bug");
        json.Should().Contain("AdaptiveCard");
    }

    [Fact]
    public void DoneCard_ContainsSummaryAndPrLink()
    {
        var card = TeamsCardBuilder.BuildDoneCard("All tests pass", "https://github.com/org/repo/pull/1");
        var json = card.ToJsonString();

        json.Should().Contain("All tests pass");
        json.Should().Contain("View Pull Request");
        json.Should().Contain("https://github.com/org/repo/pull/1");
    }

    [Fact]
    public void ErrorCard_ContainsErrorAndLogLink()
    {
        var card = TeamsCardBuilder.BuildErrorCard("Build failed", "https://logs.example.com/123");
        var json = card.ToJsonString();

        json.Should().Contain("Build failed");
        json.Should().Contain("View Logs");
    }

    [Fact]
    public void ClarificationCard_ContainsConfirmAndHelpActions()
    {
        var card = TeamsCardBuilder.BuildClarificationCard("fix ticket #42");
        var json = card.ToJsonString();

        json.Should().Contain("fix ticket #42");
        json.Should().Contain("\"answer\":\"confirm\"");
        json.Should().Contain("\"answer\":\"help\"");
    }

    [Fact]
    public void AnsweredCard_ShowsAnswerWithEmoji()
    {
        var card = TeamsCardBuilder.BuildAnsweredCard("Approve deployment?", "yes");
        var json = card.ToJsonString();

        json.Should().Contain("Approve deployment?");
        json.Should().Contain("Answered:");
        json.Should().Contain("yes");
    }

    [Fact]
    public void AllCards_HaveCorrectSchema()
    {
        var card = TeamsCardBuilder.BuildQuestionCard(CreateQuestion(QuestionType.Confirmation));
        var json = card.ToJsonString();

        json.Should().Contain("adaptivecards.io/schemas/adaptive-card.json");
        json.Should().Contain("\"version\":\"1.4\"");
        json.Should().Contain("\"type\":\"AdaptiveCard\"");
    }
}
