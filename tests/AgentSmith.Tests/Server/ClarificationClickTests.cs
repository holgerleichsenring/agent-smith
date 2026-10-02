using System.Text.Json.Nodes;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2d: a click on a clarification whose command is no longer pending — expired,
/// flushed before this phase, or already taken — answers instead of falling silent. Confirm is
/// told to send it again; Help shows help; Slack's buttons go either way.
/// </summary>
public sealed class ClarificationClickTests : IDisposable
{
    private const string Resend = "This request is no longer pending — please send it again.";

    private readonly ServerStateStore _store = new();
    private readonly RecordingHttpHandler _slackApi = new();
    private readonly List<(string Platform, string Text)> _said = [];

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task SlackInteractionHandler_ConfirmWithNothingPending_AnswersResend()
    {
        await SlackHandler().HandleAsync("C1", "clarification", "confirm", Click(), CancellationToken.None);

        _said.Should().ContainSingle().Which.Should().Be(("slack", Resend));
    }

    [Fact]
    public async Task TeamsInteractionHandler_ConfirmWithNothingPending_AnswersResend()
    {
        var value = JsonNode.Parse("""{"questionId":"clarification","answer":"confirm"}""")!;

        await new TeamsInteractionHandler(null!, Taker(), null!, null!, NullLogger<TeamsInteractionHandler>.Instance)
            .HandleAsync("19:conv", "A1", value, CancellationToken.None);

        _said.Should().ContainSingle().Which.Should().Be(("teams", Resend));
    }

    [Fact]
    public async Task SlackInteractionHandler_HelpWithNothingPending_ShowsHelpAndNoResend()
    {
        await SlackHandler().HandleAsync("C1", "clarification", "help", Click(), CancellationToken.None);

        _said.Should().ContainSingle().Which.Text.Should().Contain("here's what I can do");
    }

    [Fact]
    public async Task SlackInteractionHandler_NothingPending_StillRemovesTheButtons()
    {
        await SlackHandler().HandleAsync("C1", "clarification", "confirm", Click(), CancellationToken.None);

        _slackApi.Requests.Should().ContainSingle(r => r.Url.EndsWith("chat.update"))
            .Which.Body.Should().Contain("No longer pending");
    }

    [Fact]
    public async Task ClarificationTaker_HelpWithACommandPending_TakesItAndShowsHelp()
    {
        await Manager().SetAsync("slack", "C1", new("fix #58", "fix 58", "U1"), CancellationToken.None);

        (await Taker().TakeAsync("slack", "C1", "help", CancellationToken.None)).Should().BeNull();

        _said.Should().ContainSingle().Which.Text.Should().Contain("here's what I can do");
        (await Manager().TakeAsync("slack", "C1", CancellationToken.None)).Should().BeNull("help cleared it");
    }

    private static JsonNode Click() => JsonNode.Parse("""{"user":{"id":"U1"},"message":{"ts":"1700.01"}}""")!;

    private SlackInteractionHandler SlackHandler() => new(
        null!, Taker(), null!,
        new SlackAdapter(
            new SlackApiClient(new HttpClient(_slackApi), new SlackAdapterOptions { BotToken = "t" },
                NullLogger<SlackApiClient>.Instance),
            new SlackTypedQuestionBlockBuilder(), new SlackMessageBlockBuilder(),
            NullLogger<SlackAdapter>.Instance),
        NullLogger<SlackInteractionHandler>.Instance);

    private ClarificationTaker Taker() => new(Manager(), new HelpHandler(
        new PlatformAdapters([Adapter("slack"), Adapter("teams")], NullLogger<PlatformAdapters>.Instance),
        NullLogger<HelpHandler>.Instance));

    private ClarificationStateManager Manager() =>
        new(_store.ScopeFactory, TimeProvider.System, NullLogger<ClarificationStateManager>.Instance);

    private IPlatformAdapter Adapter(string platform)
    {
        var adapter = new Mock<IPlatformAdapter>();
        adapter.SetupGet(a => a.Platform).Returns(platform);
        adapter.Setup(a => a.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => _said.Add((platform, text)))
            .Returns(Task.CompletedTask);
        return adapter.Object;
    }
}
