using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class HumanToolHostRichChoicesTests
{
    [Fact]
    public async Task AskHuman_RichChoices_CarriedThroughTransport()
    {
        var transport = new Mock<IDialogueTransport>();
        DialogQuestion? published = null;
        transport.Setup(t => t.PublishQuestionAsync(It.IsAny<string>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<string, DialogQuestion, CancellationToken>((_, q, _) => published = q);
        transport.Setup(t => t.WaitForAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialogAnswer("q", "A", null, DateTimeOffset.UtcNow, "user"));
        var host = new HumanToolHost(transport.Object, jobId: "job-1");

        var choices = new List<HumanToolHost.AskHumanChoice>
        {
            new("Approve", "Ship it"),
            new("Reject", "Block the change")
        };
        await host.AskHuman("Ship?", context: "Risky migration", choices: choices, recommended_index: 0);

        published.Should().NotBeNull();
        published!.Choices.Should().HaveCount(2);
        published.Choices![0].Label.Should().Be("Approve");
        published.Choices[0].Description.Should().Be("Ship it");
        published.RecommendedIndex.Should().Be(0);
    }

    [Fact]
    public async Task AskHuman_NoChoices_TypeIsFreeText()
    {
        var transport = new Mock<IDialogueTransport>();
        DialogQuestion? published = null;
        transport.Setup(t => t.PublishQuestionAsync(It.IsAny<string>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<string, DialogQuestion, CancellationToken>((_, q, _) => published = q);
        transport.Setup(t => t.WaitForAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);
        var host = new HumanToolHost(transport.Object, jobId: "job-1");

        await host.AskHuman("Anything to add?");

        published!.Type.Should().Be(QuestionType.FreeText);
        published.RecommendedIndex.Should().BeNull();
    }

    [Fact]
    public async Task AskHuman_WithoutAnswerer_IsTheTargets_AndAnUnknownOneIsRefused()
    {
        var transport = new Mock<IDialogueTransport>();
        DialogQuestion? published = null;
        transport.Setup(t => t.PublishQuestionAsync(It.IsAny<string>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()))
            .Callback<string, DialogQuestion, CancellationToken>((_, q, _) => published = q);
        var host = new HumanToolHost(transport.Object, jobId: "job-1");

        await host.AskHuman("Anything to add?");
        published!.Answerers.Should().Equal(AgentSmith.Domain.Models.QuestionAnswerers.Target);
        await host.AskHuman("Which payload?", answerer: "plan");
        published.Answerers.Should().Equal(AgentSmith.Domain.Models.QuestionAnswerers.Plan);
        published = null;

        (await host.AskHuman("Proceed?", answerer: "boss")).Should().Contain("'boss' is not an answerer");
        published.Should().BeNull("a refused question is not published");
    }
}
