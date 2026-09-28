using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>
/// A run started from a chat thread, followed back to that thread through the production
/// composition over a real store: its question, the answer to it, and its outcome — across a
/// restart and with nothing but the database carried over.
/// </summary>
public sealed class ChatRunBindingTests : IDisposable
{
    private static readonly ChatThread SlackThread = new("slack", "C1", "1700000000.0001", "U1");
    private static readonly ChatThread TeamsThread = new("teams", "19:conv", "19:conv", "A1");
    private readonly ChatRunHarness _chat = new();

    public void Dispose() => _chat.Dispose();

    [Fact]
    public async Task ChatBoundRun_Terminal_PostsOutcomeWithPrUrl_AndFreesThread()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        _chat.SeedRun("run-1", "success", finished: true, "Fixed the parser.",
            "https://github.com/acme/app/pull/7");

        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        var (thread, text) = _chat.Slack.Posts.Last();
        thread.ThreadId.Should().Be(SlackThread.ThreadId, "the outcome answers in the thread that asked");
        text.Should().Contain("run-1").And.Contain("https://github.com/acme/app/pull/7");
        (await Store(_chat).FindOpenInThreadAsync("slack", "C1", SlackThread.ThreadId, default))
            .Should().BeNull("the thread is free once the outcome is posted");
        await StartAsync(_chat, SlackThread, "run-2");
        _chat.Slack.Posts.Last().Text.Should().Contain("Queued").And.Contain("run-2");
    }

    [Fact]
    public async Task ChatBoundRun_Failed_PostsReason()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        _chat.SeedRun("run-1", "failed", finished: true, "The build broke in step 7.");

        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        _chat.Slack.Posts.Where(p => p.Text.Contains("failed")).Should().ContainSingle(
            "a closed binding is not reported twice").Which.Text.Should().Contain("The build broke in step 7.");
    }

    [Fact]
    public async Task ChatBoundRun_Question_PostsToThread_AnswerResumesRun()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        _chat.SeedRun("run-1", "running", finished: false);
        _chat.LiveQuestions.Latest["run-1"] = Choice("q-7");

        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        _chat.Slack.Questions.Should().ContainSingle("a question is posted once")
            .Which.Thread.ThreadId.Should().Be(SlackThread.ThreadId);
        (await _chat.Get<ChatRunAnswerRouter>().TryAnswerAsync(SlackThread, "q-7", "1", null, default))
            .Should().BeTrue();
        var answer = await _chat.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-7", default);
        answer!.Answer.Should().Be("Split the module", "a choice button answers by index, the run asked by label");
        _chat.HotStream.Answers.Should().ContainSingle(a => a.JobId == "run-1", "a live wait hears it too");
    }

    [Fact]
    public async Task ChatBinding_SurvivesRestart_Resubscribes()
    {
        await StartAsync(_chat, TeamsThread, "run-1");
        using var restarted = new ChatRunHarness(_chat.Connection);
        restarted.SeedRun("run-1", "waiting_for_input", finished: false);
        await restarted.ParkAsync("run-1", FreeText("q-1"));

        await restarted.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        var (thread, question) = restarted.Teams.Questions.Should().ContainSingle().Subject;
        question.QuestionId.Should().Be("q-1", "the parked checkpoint outlives the live stream");
        thread.ReplyEndpoint.Should().Be("https://smba.example/emea",
            "the Teams address the run was started under is carried in the binding");
        restarted.Slack.Questions.Should().BeEmpty("a Teams thread is answered on Teams");
    }

    [Fact]
    public async Task ChatAnswer_AfterConversationStateExpired_StillReachesRun()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        _chat.SeedRun("run-1", "waiting_for_input", finished: false);
        await _chat.ParkAsync("run-1", FreeText("q-2"));
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        using var later = new ChatRunHarness(_chat.Connection);
        var answered = await later.Get<ChatRunAnswerRouter>()
            .TryAnswerFromMessageAsync(SlackThread, "Use the v2 endpoint", default);

        answered.Should().BeTrue("the binding, not a conversation state, names the run");
        (await later.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-2", default))!
            .Answer.Should().Be("Use the v2 endpoint");
    }

    [Fact]
    public async Task ChatThread_WithAnOpenRun_RefusesASecondRun()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        var launched = false;

        await _chat.Get<ChatRunStart>().StartAsync(SlackThread, "code for ticket #9",
            _ => { launched = true; return Task.FromResult(ChatLaunchResult.Started("run-2")); }, default);

        launched.Should().BeFalse();
        _chat.Slack.Posts.Last().Text.Should().Contain("run-1").And.Contain("has not reported back");
    }

    [Fact]
    public async Task ChatBoundRun_NoRecordLongAfterBinding_IsReportedGone()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);
        _chat.Slack.Posts.Should().ContainSingle("a run with no record yet is still starting");

        _chat.Clock.Now += ChatRunFollower.GoneAfter + TimeSpan.FromMinutes(1);
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None);

        _chat.Slack.Posts.Last().Text.Should().Contain("left no record");
    }

    [Fact]
    public async Task ChatBoundRun_FollowedByTwoReplicas_IsReportedOnce()
    {
        await StartAsync(_chat, SlackThread, "run-1");
        using var replica = new ChatRunHarness(_chat.Connection);
        _chat.SeedRun("run-1", "success", finished: true);

        await Task.WhenAll(
            _chat.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None),
            replica.Get<ChatRunFollower>().FollowOnceAsync(CancellationToken.None));

        _chat.Slack.Posts.Concat(replica.Slack.Posts).Count(p => p.Text.Contains("finished"))
            .Should().Be(1, "closing the binding is the claim to report it");
    }

    [Fact]
    public async Task ChatLaunch_Announcement_ReadsTheDashboardUrlLive()
    {
        _chat.Config.Dialogue.DashboardUrl = "https://agents.example";

        await StartAsync(_chat, SlackThread, "run-1");

        _chat.Slack.Posts.Single().Text.Should().Contain("https://agents.example/jobs/run-1",
            "the URL set after startup is the one the reply names");
    }

    internal static Task StartAsync(ChatRunHarness chat, ChatThread thread, string runId) =>
        chat.Get<ChatRunStart>().StartAsync(thread, "code for ticket #42",
            _ => Task.FromResult(ChatLaunchResult.Started(runId)), CancellationToken.None);

    private static IChatRunBindingStore Store(ChatRunHarness chat) => chat.Get<IChatRunBindingStore>();

    private static DialogQuestion Choice(string id) => new(
        id, QuestionType.Choice, "How should the parser be fixed?", null,
        [new DialogChoice("Patch in place"), new DialogChoice("Split the module")], null, TimeSpan.FromHours(1));

    private static DialogQuestion FreeText(string id) => new(
        id, QuestionType.FreeText, "Which endpoint?", null, null, null, TimeSpan.FromDays(3));
}
