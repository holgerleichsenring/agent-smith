using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-13-9a59b: the record refresh over a real migrated store; the host behind a scripted
/// provider, trust and the ticket-activity nudge mocked.
/// </summary>
internal sealed class PrReviewRefreshHarness : IDisposable
{
    public const string Pr1 = "https://github.com/o/api/pull/1";
    public const string Pr2 = "https://github.com/o/api/pull/2";
    public static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    public PrReviewRefreshHarness(Action<IServiceCollection>? configure = null)
    {
        Store = new ServerStateStore(Clock, configure: services =>
        {
            ClosedTicketFollowUps.Register(services);
            configure?.Invoke(services);
        });
        Provider.As<IPrReviewStateReader>();
        Provider.As<IPrReviewThreadReader>();
        Sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(() => Provider.Object);
        Trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepoType _, PrCommentAuthor a, CancellationToken _) => a.AuthorLogin != "stranger");
        Threads([]);
    }

    public SettableClock Clock { get; } = new() { Now = T.AddHours(1) };
    public ServerStateStore Store { get; }
    public Mock<ISourceProvider> Provider { get; set; } = new();
    public Mock<ISourceProviderFactory> Sources { get; } = new();
    public Mock<IPrReviewAuthorTrust> Trust { get; } = new();
    public Mock<ITicketActivityNudge> Activity { get; } = new();
    public Mock<ISystemEventPublisher> Events { get; } = new();

    public static ResolvedProject Project { get; } = new()
    {
        Name = "p", Tracker = new TrackerConnection { Name = "gh-tracker", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api", Type = RepoType.GitHub, Url = "https://github.com/o/api" }],
    };

    public PrReviewRecordRefresh Refresh() => new(Store.ScopeFactory, Sources.Object, Trust.Object, new PrReviewEntryBuilder(),
        Recorder(), Activity.Object, Clock,
        NullLogger<PrReviewRecordRefresh>.Instance);

    // 2026-10-14-dbceg: the landing recorder over the harness store.
    public TicketLandingRecorder Recorder() => new(Store.ScopeFactory, new LandedTicketRepository(Store.ScopeFactory), Wake(),
        Events.Object, Activity.Object, Clock, NullLogger<TicketLandingRecorder>.Instance);

    // 2026-10-14-dbceh: the wake over the harness store and its nudges; attempts, leases and approvals are the harness's.
    public Mock<IPreviousAttemptReader> Attempts { get; } = new();
    public Mock<IActiveRunLease> Leases { get; } = new();
    public InMemorySpecApprovalStore Approvals { get; } = new();

    public DependentsWake Wake() =>
        WakeOver(Store, Store.Services.GetRequiredService<IReworkNudges>(), Attempts.Object, Leases.Object, Approvals, Clock);

    public static DependentsWake WakeOver(ServerStateStore store, IReworkNudges nudges, IPreviousAttemptReader? attempts = null,
        IActiveRunLease? leases = null, ISpecApprovalStore? approvals = null, TimeProvider? clock = null)
    {
        var requirements = new TicketRequirementRepository(store.ScopeFactory);
        var landings = new LandedTicketRepository(store.ScopeFactory);
        return new(requirements, landings, new UnmetTicketRequirements(requirements, landings, approvals ?? new InMemorySpecApprovalStore()),
            attempts ?? Mock.Of<IPreviousAttemptReader>(), leases ?? Mock.Of<IActiveRunLease>(), nudges, clock ?? TimeProvider.System,
            NullLogger<DependentsWake>.Instance);
    }

    public List<LandedTicket> Landings() => [.. Store.Context().Set<LandedTicket>().AsNoTracking().OrderBy(l => l.Id)];

    public Task RefreshAsync(string? nudged = null) => Refresh().RefreshAsync(Project, "7", nudged, CancellationToken.None);

    public void State(string url, string state, params PrReviewDecision[] decisions) =>
        Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState(url.Split('/')[^1], state, T, decisions));

    public void Threads(IReadOnlyList<PrReviewThread> threads) =>
        Provider.As<IPrReviewThreadReader>().Setup(s => s.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

    public static PrCommentAuthor Author(string login) => new("https://github.com/o/api", "o/api", login, login);

    public static PrReviewNote Note(string hostId, string login, string body, bool isSystem = false) =>
        new(Author(login), false, isSystem, T.AddMinutes(5), body) { HostId = hostId };

    public static PrReviewDecision Decision(string hostId, string login, string decision) =>
        new(hostId, Author(login), false, decision, T.AddMinutes(5), string.Empty);

    public async Task RunAsync(string runId, params string[] urls)
    {
        await using var ctx = Store.Context();
        ctx.Add(new Run
        {
            Id = runId, Project = "p", TicketId = "7", Pipeline = "code", Status = "success", StartedAt = T, FinishedAt = T,
            PullRequestsJson = RunStoryJson.Serialize(urls.Select(u => new RunPullRequestView("api", PullRequestStatuses.Opened, u, null, T)).ToList()),
        });
        await ctx.SaveChangesAsync();
    }

    public List<PrReviewRecord> Records() => [.. Store.Context().Set<PrReviewRecord>().AsNoTracking().OrderBy(r => r.Id)];

    public List<PrReviewEntry> Entries() => [.. Store.Context().Set<PrReviewEntry>().AsNoTracking().OrderBy(e => e.Id)];

    public void Dispose() => Store.Dispose();
}
