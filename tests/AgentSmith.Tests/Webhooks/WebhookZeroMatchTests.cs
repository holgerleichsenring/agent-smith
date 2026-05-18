using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// p0140b: zero-match branch of <see cref="WebhookSpawnDispatcher"/>.
///
/// Confirms: empty matches list never invokes <see cref="ISpawnPipelineRunsUseCase.ExecuteAsync"/>;
/// opt-in (TrackerConnection.ZeroMatchComment=true) triggers
/// <see cref="ITicketProvider.UpdateStatusAsync"/> when the provider supports comments;
/// providers that don't support comments stay quiet (no exception, no call).
/// </summary>
public sealed class WebhookZeroMatchTests
{
    [Theory]
    [InlineData("github")]
    [InlineData("gitlab")]
    [InlineData("azuredevops")]
    [InlineData("jira")]
    public async Task ZeroMatch_AllPlatforms_NoSpawnCalled(string platform)
    {
        var (sut, spawn, factory) = BuildSut(new AgentSmithConfig());
        var envelope = new IncomingTicketEnvelope { TicketId = "42", Platform = platform };

        await sut.DispatchAsync(
            new AgentSmithConfig(), Array.Empty<ProjectMatch>(), envelope,
            ticketStatus: "open", planAnswers: null, ct: CancellationToken.None);

        spawn.Verify(s => s.ExecuteAsync(
            It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
            It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
            It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()), Times.Never);
        // Without a tracker opted in, the provider factory should not be called either.
        factory.Verify(f => f.Create(It.IsAny<TrackerConnection>()), Times.Never);
    }

    [Fact]
    public async Task WebhookHandler_ZeroMatch_TrackerOptedIn_AlsoWritesComment()
    {
        var tracker = new TrackerConnection { Type = TrackerType.GitHub, ZeroMatchComment = true };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["t"] = tracker }
        };

        var (sut, _, factory) = BuildSut(config, supportsComments: true, out var provider);

        await sut.DispatchAsync(
            config, Array.Empty<ProjectMatch>(),
            new IncomingTicketEnvelope { TicketId = "42", Platform = "github" },
            ticketStatus: "open", planAnswers: null, ct: CancellationToken.None);

        factory.Verify(f => f.Create(tracker), Times.Once);
        provider.Verify(p => p.UpdateStatusAsync(
            It.Is<TicketId>(t => t.Value == "42"),
            It.Is<string>(s => s.Contains("agent-smith")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WebhookHandler_ZeroMatch_TrackerOptedIn_ButSupportsCommentsFalse_LogsOnlyNoCrash()
    {
        var tracker = new TrackerConnection { Type = TrackerType.GitHub, ZeroMatchComment = true };
        var config = new AgentSmithConfig
        {
            Trackers = new Dictionary<string, TrackerConnection> { ["t"] = tracker }
        };

        var (sut, _, _) = BuildSut(config, supportsComments: false, out var provider);

        var act = async () => await sut.DispatchAsync(
            config, Array.Empty<ProjectMatch>(),
            new IncomingTicketEnvelope { TicketId = "42", Platform = "github" },
            ticketStatus: "open", planAnswers: null, ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
        provider.Verify(p => p.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (WebhookSpawnDispatcher sut,
                    Mock<ISpawnPipelineRunsUseCase> spawn,
                    Mock<ITicketProviderFactory> factory)
        BuildSut(AgentSmithConfig _)
    {
        var (sut, spawn, factory, _) = BuildSutFull(supportsComments: true);
        return (sut, spawn, factory);
    }

    private static (WebhookSpawnDispatcher sut,
                    Mock<ISpawnPipelineRunsUseCase> spawn,
                    Mock<ITicketProviderFactory> factory)
        BuildSut(AgentSmithConfig _, bool supportsComments, out Mock<ITicketProvider> provider)
    {
        var bundle = BuildSutFull(supportsComments);
        provider = bundle.provider;
        return (bundle.sut, bundle.spawn, bundle.factory);
    }

    private static (WebhookSpawnDispatcher sut,
                    Mock<ISpawnPipelineRunsUseCase> spawn,
                    Mock<ITicketProviderFactory> factory,
                    Mock<ITicketProvider> provider)
        BuildSutFull(bool supportsComments)
    {
        var spawn = new Mock<ISpawnPipelineRunsUseCase>();
        spawn.Setup(s => s.ExecuteAsync(
                It.IsAny<AgentSmithConfig>(), It.IsAny<ResolvedProject>(), It.IsAny<string>(),
                It.IsAny<IncomingTicketEnvelope>(), It.IsAny<WebhookTriggerConfig>(),
                It.IsAny<CancellationToken>(), It.IsAny<Dictionary<string, string>?>()))
            .ReturnsAsync(new SpawnResult(Array.Empty<ClaimResult>()));

        var provider = new Mock<ITicketProvider>();
        provider.SetupGet(p => p.SupportsComments).Returns(supportsComments);
        provider.Setup(p => p.UpdateStatusAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);

        var sut = new WebhookSpawnDispatcher(
            spawn.Object, factory.Object, NullLogger<WebhookSpawnDispatcher>.Instance);
        return (sut, spawn, factory, provider);
    }
}
