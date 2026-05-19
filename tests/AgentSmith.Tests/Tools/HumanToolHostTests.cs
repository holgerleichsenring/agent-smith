using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class HumanToolHostTests
{
    [Fact]
    public void GetTools_AnyPhase_ReturnsAskHumanTool()
    {
        var host = new HumanToolHost();

        foreach (var phase in Enum.GetValues<SkillExecutionPhase>())
        {
            var names = host.GetTools(phase, null).Select(f => f.Name).ToList();
            names.Should().BeEquivalentTo(new[] { "ask_human" }, $"phase {phase} should expose only ask_human");
        }
    }

    [Fact]
    public async Task AskHuman_DelegatesToDialogueTransport()
    {
        var transport = new Mock<IDialogueTransport>();
        transport
            .Setup(t => t.WaitForAnswerAsync("job-1", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialogAnswer("qid", "yes", null, DateTimeOffset.UtcNow, "operator"));
        var host = new HumanToolHost(transport.Object, jobId: "job-1");

        var result = await host.AskHuman("Proceed?");

        result.Should().Be("Answer: yes");
        transport.Verify(
            t => t.PublishQuestionAsync("job-1", It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskHuman_NoTransportConfigured_ReturnsStructuredError()
    {
        var host = new HumanToolHost();

        var result = await host.AskHuman("Proceed?");

        result.Should().StartWith("Error:").And.Contain("Dialogue transport not configured");
    }
}
