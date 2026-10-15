using System.Security.Claims;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.Rework.Records;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tickets.LandedByHand;

/// <summary>
/// 2026-10-14-dbcei: the landed-by-hand endpoint over a real migrated store — the recorder, its wake, the
/// requirement and landing rows real; the tracker a fake that knows every ticket unless told to fail; the
/// announcement, the page nudge and the rework nudges captured.
/// </summary>
internal sealed class LandedByHandFixture : IDisposable
{
    public const string Tag = "agentsmith-p";
    public const string Tracker = "gh-tracker";
    public static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    public LandedByHandFixture(Func<AgentSmithDbContext, IUnitOfWork>? unitOfWork = null)
    {
        Store = new ServerStateStore(Clock, unitOfWork);
        Nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ReworkNudgeRequest r, CancellationToken _) => Nudged.Add(r)).Returns(Task.CompletedTask);
        Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Config);
    }

    public SettableClock Clock { get; } = new() { Now = T.AddHours(1) };
    public ServerStateStore Store { get; }
    public Mock<IReworkNudges> Nudges { get; } = new();
    public List<ReworkNudgeRequest> Nudged { get; } = [];
    public Mock<ISystemEventPublisher> Events { get; } = new();
    public Mock<ITicketActivityNudge> Activity { get; } = new();
    public InMemorySpecApprovalStore Approvals { get; } = new();
    public FakeTicketTracker Tickets { get; } = new() { Status = "open" };
    public Mock<IConfigurationLoader> Loader { get; } = new();

    public static ResolvedProject Project { get; } = new()
    {
        Name = "p", DefaultPipeline = "code",
        Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }],
        GithubTrigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code", TriggerStatuses = ["open"],
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = Tag },
        },
    };

    public AgentSmithConfig Config { get; } = new()
    {
        Projects = new Dictionary<string, ResolvedProject> { ["p"] = Project }, PipelineTriggers = PipelineTriggerMap.Empty,
    };

    public static ClaimsPrincipal Ada { get; } = Person(("sub", "u-ada"), ("name", "Ada Lovelace"));

    public LandedTicketRepository Landings => new(Store.ScopeFactory);
    public TicketRequirementRepository Requirements => new(Store.ScopeFactory);
    public UnmetTicketRequirements Unmet => new(Requirements, Landings, Approvals);

    public TicketLandingRecorder Recorder() => new(Store.ScopeFactory, Landings,
        PrReviewRefreshHarness.WakeOver(Store, Nudges.Object, approvals: Approvals, clock: Clock),
        Events.Object, Activity.Object, Clock, NullLogger<TicketLandingRecorder>.Instance);

    public static ClaimsPrincipal Person(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    public Task<IResult> MarkAsync(
        string note = "Shipped in the release branch", string ticket = "7", ClaimsPrincipal? user = null,
        string project = "p", AgentSmith.Contracts.Providers.ITicketProviderFactory? trackers = null, CancellationToken ct = default) =>
        TicketLandedByHandEndpoints.LandedByHandAsync(project, ticket, new LandedByHandRequest(note),
            new DefaultHttpContext { User = user ?? Ada }, new TicketProjects(Loader.Object, new ServerContext("c.yml")),
            trackers ?? Tickets, Landings, Recorder(), ct);

    public static (int Status, string Body) Said(IResult result) => result is ContentHttpResult content
        ? (content.StatusCode ?? StatusCodes.Status200OK, content.ResponseContent ?? string.Empty)
        : throw new InvalidOperationException($"not a sentence: {result.GetType().Name}");

    public static TicketLandingFact Landed(IResult result) => ((Ok<TicketLandingFact>)result).Value!;

    public List<LandedTicket> Rows() => [.. Store.Context().Set<LandedTicket>().OrderBy(l => l.Id)];

    /// <summary>A dependent waiting for <paramref name="required"/>, starting when met, its filing complete.</summary>
    public async Task HeldAsync(string dependent, string required)
    {
        await using (var db = Store.Context())
        {
            db.Add(new TicketRequirement
            {
                Tracker = Tracker, TicketKey = $"github-{dependent}", TicketId = dependent, Project = "p",
                RequiredTicketKey = $"github-{required}", RequiredTicketId = required, StartsWhenMet = true, FiledInConversation = "s-1",
            });
            await db.SaveChangesAsync();
        }
        await Approvals.SaveAsync(ApprovedSets.Record($"github-{dependent}", T, tracker: Tracker), default);
    }

    public Task<bool> MergedAsync(string ticket) => Landings.TryRecordAsync(new TicketLandingFact(
        Tracker, $"github-{ticket}", ticket, "p", TicketLandingKinds.Merged, T, null, null, "https://github.com/o/api/pull/1"), default);

    public void Dispose() => Store.Dispose();
}
