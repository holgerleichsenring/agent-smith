using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// What the three PR-comment handler test classes share: the admission with a real
/// <see cref="CommentIntentParser"/> over a stubbed model (so a test can prove the model
/// was never asked), and a repo lookup that either declares the commented repository or not.
/// </summary>
internal sealed class PrCommentHandlerFixture
{
    public Mock<IIntentParser> Model { get; } = new();

    public PrCommentHandlerFixture()
    {
        Model.Setup(p => p.ParseToPipelineRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, string _, CancellationToken _) => Resolve(input));
    }

    /// <summary>2026-10-08-e8b9e: what the admission launched — the launch itself is PrCommandLaunch's.</summary>
    public List<(PrCommentCommand Command, PipelineRequest Request)> Launched { get; } = [];

    public PrCommentCommandAdmission Admission()
    {
        var launch = new Mock<AgentSmith.Server.Contracts.IPrCommandLaunch>();
        launch.Setup(l => l.LaunchAsync(It.IsAny<PrCommentCommand>(), It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlySet<string>?>()))
            .Callback<PrCommentCommand, PipelineRequest, CancellationToken, IReadOnlySet<string>?>((c, r, _, _) => Launched.Add((c, r)))
            .ReturnsAsync(WebhookResult.HandledNoRoute());
        return new(new CommentIntentParser(Model.Object), new ServerContext("config.yml"), launch.Object,
            NullLogger<PrCommentCommandAdmission>.Instance);
    }

    /// <summary>The launched command as the old trigger input spelled it: pipeline, ticket, PR reference.</summary>
    public string? LaunchedAs() => Launched.Count == 0 ? null
        : $"{Launched[^1].Request.PipelineName}{(Launched[^1].Request.TicketId is { } t ? $" #{t.Value}" : "")} {Launched[^1].Command.PrReference}";

    public static IPrCommentRepoLookup Repos(string? configuredUrl)
    {
        var lookup = new Mock<IPrCommentRepoLookup>();
        if (configuredUrl is not null)
            lookup.Setup(l => l.Find(It.IsAny<string>())).Returns(new ConfiguredRepo(
                "test-project", new ResolvedProject(), new RepoConnection { Name = "repo", Url = configuredUrl }));
        return lookup.Object;
    }

    public static IPrCommentTrustVerdictCache NewCache() => new PrCommentTrustVerdictCache(TimeProvider.System);

    public void VerifyModelNeverAsked() =>
        Model.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    private static PipelineRequest Resolve(string body) =>
        new("test-project", body.Contains("security", StringComparison.OrdinalIgnoreCase) ? "security-scan" : "code",
            TicketId: body.Contains("#99") ? new AgentSmith.Domain.Models.TicketId("99") : null, Headless: true);
}
