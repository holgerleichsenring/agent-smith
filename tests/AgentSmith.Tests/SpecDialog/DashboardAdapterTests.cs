using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-9033: the dashboard channel as a platform adapter. What it delivers goes to
/// the one session group it belongs to; what it cannot carry is said out loud.
/// </summary>
public sealed class DashboardAdapterTests
{
    private const string Dialog = "d-7f3a";

    private readonly RecordingDialogHub _hub = new();
    private readonly CapturingLogger<DashboardAdapter> _logger = new();

    [Fact]
    public void Adapter_Platform_IsDistinctFromEveryOther()
    {
        var adapter = NewAdapter();

        adapter.Platform.Should().NotBe(DispatcherDefaults.PlatformSlack)
            .And.NotBe(DispatcherDefaults.PlatformTeams);
        // The messenger keys its adapter dictionary on Platform and throws on a duplicate,
        // which would take the whole spec dialog down at startup rather than in the one
        // conversation that collided.
        var act = () => new SpecDialogMessenger(
            [Chat(DispatcherDefaults.PlatformSlack), Chat(DispatcherDefaults.PlatformTeams), adapter],
            NullLogger<SpecDialogMessenger>.Instance);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Adapter_SendInfo_ReachesOnlyTheSessionGroup()
    {
        await NewAdapter().SendInfoAsync(
            Dialog, "Spec dialog", "the design so far", Dialog, CancellationToken.None);

        var push = _hub.Pushes.Should().ContainSingle().Subject;
        push.Group.Should().Be(HubGroups.SpecDialog(Dialog));
        push.Method.Should().Be("SpecDialogMessage");
        push.Args.Single().Should().BeOfType<SpecDialogChannelMessage>()
            .Which.Text.Should().Be("the design so far");
    }

    [Fact]
    public async Task Adapter_AskTypedQuestion_PushesTheQuestion()
    {
        var question = new DialogQuestion(
            "q-1", QuestionType.Approval, "File these two tickets?",
            Context: null, Choices: null, DefaultAnswer: "", TimeSpan.FromMinutes(15));

        var answer = await NewAdapter().AskTypedQuestionAsync(
            Dialog, question, Dialog, CancellationToken.None);

        var push = _hub.Pushes.Should().ContainSingle().Subject;
        push.Group.Should().Be(HubGroups.SpecDialog(Dialog));
        push.Method.Should().Be("SpecDialogQuestion");
        push.Args.Single().Should().BeOfType<SpecDialogChannelQuestion>()
            .Which.QuestionId.Should().Be("q-1");
        answer.Should().BeNull(
            "the affordance pushes but does not block — the session's next message is the "
            + "answer, through the router's pending-question branch");
    }

    [Fact]
    public async Task Adapter_UnusedMethod_IsNeverASilentSuccess()
    {
        var adapter = NewAdapter();

        await adapter.SendProgressAsync(Dialog, 1, 4, "fix-bug", CancellationToken.None);
        await adapter.SendDoneAsync(Dialog, "done", null, CancellationToken.None);
        await adapter.SendErrorAsync(Dialog, new ErrorContext(
            "j-1", Dialog, "T-1", "sample", 1, 4, "Analyze", "raw", "friendly", null), CancellationToken.None);
        await adapter.UpdateQuestionAnsweredAsync(Dialog, "m-1", "q?", "yes", CancellationToken.None);
        await adapter.SendDetailAsync(Dialog, "tool call", CancellationToken.None);
        await adapter.SendClarificationAsync(Dialog, "did you mean", CancellationToken.None);

        _hub.Pushes.Should().BeEmpty("no run-trigger conversation exists on this platform");
        _logger.Warnings.Should().HaveCount(6,
            "an arrival on a method this channel does not carry is a finding, not a "
            + "success nobody can see");
    }

    [Fact]
    public async Task Adapter_WithoutTheDashboardApi_SaysSoInsteadOfDelivering()
    {
        // The dashboard API is env-gated; the adapter is composed either way, because the
        // messenger enumerates every adapter when it is built.
        var adapter = new DashboardAdapter(_logger);

        await adapter.SendInfoAsync(Dialog, "Spec dialog", "text", Dialog, CancellationToken.None);

        _logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain(Dialog);
    }

    private DashboardAdapter NewAdapter() => new(_logger, _hub);

    private static IPlatformAdapter Chat(string platform)
    {
        var adapter = new Mock<IPlatformAdapter>();
        adapter.SetupGet(a => a.Platform).Returns(platform);
        return adapter.Object;
    }
}
