using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>
/// A run's replies reach the platform its thread is on, inside that thread — Slack by
/// thread_ts, Teams by the conversation and the service URL the binding carried.
/// </summary>
public sealed class ChatThreadAdapterTests
{
    private static readonly DialogQuestion Question = new(
        "q1", QuestionType.Confirmation, "Merge it?", null, null, null, TimeSpan.FromHours(1));

    private readonly RecordingHttpHandler _http = new();

    [Fact]
    public async Task SlackThreadAdapter_PostsTextAndQuestionsUnderTheThread()
    {
        var adapter = new SlackThreadAdapter(Slack(), new SlackTypedQuestionBlockBuilder());
        var thread = new ChatThread("slack", "C1", "1700.01", "U1");

        await adapter.PostAsync(thread, "hello", default);
        await adapter.PostQuestionAsync(thread, Question, default);

        _http.Requests.Should().HaveCount(2).And.OnlyContain(r => r.Body.Contains("\"thread_ts\":\"1700.01\""));
        _http.Requests[1].Body.Should().Contain("q1:yes", "the button carries the question id back");
        adapter.ReplyEndpointFor("C1").Should().BeNull();
    }

    [Fact]
    public async Task TeamsThreadAdapter_PostsThroughTheServiceUrlTheBindingCarried()
    {
        var urls = new TeamsServiceUrls();
        var adapter = new TeamsThreadAdapter(Teams(urls), urls,
            new TeamsCardBuilder(new TeamsQuestionCardBuilder(), new TeamsStatusCardBuilder()));
        var thread = new ChatThread("teams", "19:conv", "19:conv", "A1", "https://smba.example/emea/");

        await adapter.PostQuestionAsync(thread, Question, default);

        _http.Requests.Should().Contain(r => r.Url == "https://smba.example/emea/v3/conversations/19:conv/activities");
        adapter.ReplyEndpointFor("19:conv").Should().Be("https://smba.example/emea");
    }

    [Fact]
    public async Task ChatThreadAdapters_RouteByThePlatformRecorded_AndSkipAnUnknownOne()
    {
        var slack = new RecordingThreadAdapter("slack");
        var teams = new RecordingThreadAdapter("teams", "https://smba.example");
        var adapters = new ChatThreadAdapters([slack, teams], NullLogger<ChatThreadAdapters>.Instance);

        await adapters.PostAsync(new ChatThread("Teams", "19:c", "19:c", "A1"), "hi", default);
        await adapters.PostAsync(new ChatThread("whatsapp", "W1", null, "P1"), "hi", default);

        teams.Posts.Should().ContainSingle();
        slack.Posts.Should().BeEmpty("a Teams thread never goes out through Slack");
        adapters.ReplyEndpointFor("teams", "19:c").Should().Be("https://smba.example");
        adapters.ReplyEndpointFor("whatsapp", "W1").Should().BeNull();
    }

    private SlackApiClient Slack() => new(
        new HttpClient(_http), new SlackAdapterOptions { BotToken = "t" }, NullLogger<SlackApiClient>.Instance);

    private TeamsApiClient Teams(TeamsServiceUrls urls) => new(
        new HttpClient(_http),
        new BotFrameworkTokenProvider(new HttpClient(_http), new TeamsAdapterOptions { AppId = "a", AppPassword = "p" }),
        urls, NullLogger<TeamsApiClient>.Instance);
}
