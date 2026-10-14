using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>The rework nudges a ticket-branch command enqueued.</summary>
    public List<ReworkNudgeRequest> Nudged { get; } = [];

    /// <summary>The nudges that carry an act — every one but the record refreshes.</summary>
    public List<ReworkNudgeRequest> Acts() => [.. Nudged.Where(n => n.Origin != ReworkNudgeOrigin.Refresh)];

    /// <summary>The project that owns the ticket branch's pull request; null: no owner.</summary>
    public string? TicketOwner { get; set; } = "test-project";

    /// <summary>The head an issue comment's command is completed with (GitHub reads it from the host).</summary>
    public string? HeadRef { get; set; } = "feature/other";

    /// <summary>The store a head-less delivery finds its ticket's record in; none by default.</summary>
    public IServiceScopeFactory? Scopes { get; set; }

    public PrCommentCommandAdmission Admission()
    {
        var launch = new Mock<AgentSmith.Server.Contracts.IPrCommandLaunch>();
        launch.Setup(l => l.LaunchAsync(It.IsAny<PrCommentCommand>(), It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlySet<string>?>()))
            .Callback<PrCommentCommand, PipelineRequest, CancellationToken, IReadOnlySet<string>?>((c, r, _, _) => Launched.Add((c, r)))
            .ReturnsAsync(WebhookResult.HandledNoRoute());
        var owners = new Mock<IPrTicketOwner>();
        owners.Setup(o => o.FindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlySet<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => TicketOwner is null ? null
                : new ConfiguredRepo(TicketOwner, new ResolvedProject(), new RepoConnection { Name = "repo" }));
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => Nudged.Add(r)).Returns(Task.CompletedTask);
        return new(new CommentIntentParser(Model.Object), new ServerContext("config.yml"), launch.Object,
            owners.Object, nudges.Object, PrRecordNudges.Of(owners.Object, nudges.Object, Scopes), NullLogger<PrCommentCommandAdmission>.Instance);
    }

    /// <summary>A head reader that completes a command with <see cref="HeadRef"/> in the same repository.</summary>
    public IPrCommandHead Heads()
    {
        var heads = new Mock<IPrCommandHead>();
        heads.Setup(h => h.CompleteAsync(It.IsAny<PrCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrCommentCommand c, CancellationToken _) => c.HeadRef is not null ? c
                : c with { HeadRef = HeadRef, SameRepository = true, PrUrl = $"https://github.com/org/my-api/pull/{c.PrNumber}" });
        return heads.Object;
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
