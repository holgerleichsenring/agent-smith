using System.Security.Claims;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-19-1b72a: the head's Pause and Resume reach the series of the caller's own conversation only, refuse
/// a conversation with no running series and an unknown act, and answer the series as its pane now shows it.</summary>
public sealed class SpecDialogSeriesEndpointsTests : IDisposable
{
    private readonly ServerStateStore _state = new();
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISeriesTicketFacts> _facts = new();
    private readonly Mock<ISeriesConversation> _conversation = new();

    public SpecDialogSeriesEndpointsTests() =>
        _facts.Setup(f => f.ReadAsync("sample", It.IsAny<IReadOnlyList<SeriesTicket>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<SeriesTicket> tickets, CancellationToken _) =>
                tickets.Select(t => new SeriesTicketFact(t)).ToList());

    public void Dispose() => _state.Dispose();

    [Fact]
    public async Task SeriesAct_Pause_PausesTheSeriesAndAnswersItsView()
    {
        await SessionAsync("ada");
        await _store.OpenAsync(SeriesRecords.Running(conversation: "s-1"), default);

        var result = await ActAsync("pause", "ada");

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Paused);
        result.Should().BeOfType<Ok<SeriesRunView?>>().Which.Value!.HeadState.Should().Be("series paused");
    }

    [Fact]
    public async Task SeriesAct_ForeignDialog_IsNotFound()
    {
        await SessionAsync("ada");
        await _store.OpenAsync(SeriesRecords.Running(conversation: "s-1"), default);

        (await ActAsync("pause", "eve")).Should().BeOfType<NotFound>();
        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Running);
    }

    [Fact]
    public async Task SeriesAct_NoRunningSeries_IsAConflict()
    {
        await SessionAsync("ada");

        (await ActAsync("pause", "ada")).Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task SeriesAct_UnknownAct_IsABadRequest()
    {
        (await ActAsync("stop", "ada")).Should().BeOfType<BadRequest<string>>();
    }

    private async Task SessionAsync(string owner)
    {
        await using var context = _state.Context();
        context.Add(new SpecDialogSession
        {
            SessionId = "s-1", Platform = "dashboard", ChannelId = "d-1", ThreadId = "d-1", UserId = owner, Project = "sample",
            IsOpen = true, LastActivityAt = DateTimeOffset.UnixEpoch,
        });
        await context.SaveChangesAsync();
    }

    private async Task<Microsoft.AspNetCore.Http.IResult> ActAsync(string act, string caller)
    {
        await using var context = _state.Context();
        var sessions = new SpecDialogSessionRepository(context);
        var pause = new SeriesPause(_store, _facts.Object, Mock.Of<ISeriesTicketActs>(), _conversation.Object);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["sample"] = new() { Name = "sample" } },
        });
        var views = new SeriesRunViewReader(sessions, _store, _facts.Object,
            new TicketProjects(loader.Object, new ServerContext("c.yml")), new AgentSmithConfig(), TimeProvider.System);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", caller)], "test"));
        return await SpecDialogSeriesEndpoints.ActAsync("d-1", act, user, new SpecDialogOwnership(sessions), sessions, _store, pause, views,
            CancellationToken.None);
    }
}
