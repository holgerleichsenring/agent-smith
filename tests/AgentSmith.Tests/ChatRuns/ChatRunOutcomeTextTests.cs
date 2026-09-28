using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.ChatRuns;

public sealed class ChatRunOutcomeTextTests : IDisposable
{
    private readonly ChatRunHarness _chat = new();

    public void Dispose() => _chat.Dispose();

    [Fact]
    public void Compose_ACancelledRun_SaysHowItEndedAndLinksTheDashboard()
    {
        _chat.Config.Dialogue.DashboardUrl = "https://agents.example/";

        var text = _chat.Get<ChatRunOutcomeText>().Compose(
            "run-1", new RunOutcome("cancelled", true, "Cancelled by an operator.", []));

        text.Should().Contain("ended cancelled").And.Contain("Cancelled by an operator.")
            .And.Contain("https://agents.example/jobs/run-1");
    }

    [Fact]
    public void Compose_AFailedRunWithoutSummary_SaysNoReasonWasRecorded() =>
        _chat.Get<ChatRunOutcomeText>().Compose("run-1", new RunOutcome("failed", true, null, []))
            .Should().Contain("failed: no reason was recorded");
}
