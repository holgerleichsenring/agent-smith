using System.Security.Claims;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.SpecDialog.Continuation;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-10-13-9a59n: Retry over one migrated SQLite store — the route's service by its real registration over
/// the ticket readers, the durable nudge queue, the lease, the previous-attempt reader and the real rework
/// worker reads with the REAL rework entry and park check. The tracker is a fake and the launch a recorder:
/// what starts is "r-2". Ticket 42 of project alpha; trigger status "open", done "Done", failed "Failed".
/// </summary>
internal sealed class RetryHarness : IDisposable
{
    public const string Principal = "ops-1";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public RetryHarness()
    {
        Launch.Setup(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("r-2"));
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Config).AddSingleton<ITicketProviderFactory>(Tracker);
        services.AddSingleton<TrackerIdentity>().AddTransient<TicketClosure>().AddTransient<FiledWorkTrackerProjects>();
        services.AddSingleton<IUniqueViolationTranslator, SqliteUniqueViolationTranslator>();
        services.AddScoped<RunRepository>().AddScoped<RunRetryRequestRepository>().AddScoped<ActiveRunRepository>();
        services.AddScoped<RunDeletionRepository>();
        services.AddScoped<ReworkNudgeRepository>().AddScoped<ReworkLedgerRepository>().AddScoped<RunCheckpointRepository>();
        services.AddSingleton<IActiveRunLease, DbActiveRunLease>().AddSingleton<IRunCheckpointStore, DbRunCheckpointStore>();
        services.AddSingleton<IPreviousAttemptReader, DbPreviousAttemptReader>().AddSingleton<IReworkNudges, DbReworkNudges>()
            .AddSingleton<IDecidedDefaults, DecidedDefaults>(); // 2026-10-13-8762p
        services.AddSingleton(new TokenAuthorityConfig()).AddSingleton(TimeProvider.System);
        services.AddTicketReaders().AddRunRetry();
        // 2026-10-14-da5ed: the conversation's start — the pending edit as its approval, else a conversation Retry.
        services.AddSingleton<ISpecApprovalStore>(Approvals).AddSingleton(Activity.Object).AddScoped(_ => FiledWorkDoubles.Move());
        services.AddScoped<OperatorStartArm>().AddScoped<OperatorStart>().AddTransient<PendingSeriesEditReader>();
        services.AddScoped<PendingEditStart>().AddScoped<BoundAttemptStarts>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public FakeTicketTracker Tracker { get; } = new() { Status = "Failed" };
    public Mock<IReworkLaunch> Launch { get; } = new();
    public InMemorySpecApprovalStore Approvals { get; } = new();
    public Mock<ITicketActivityNudge> Activity { get; } = new();

    public AgentSmithConfig Config { get; } = new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new()
            {
                Name = "alpha", DefaultPipeline = "code",
                Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code", TriggerStatuses = ["open"], DoneStatus = "Done", FailedStatus = "Failed",
                },
            },
        },
    };

    public ResolvedProject Project => Config.Projects["alpha"];

    public IServiceProvider Services => _provider;

    /// <summary>A finished code attempt of ticket 42 that started a step.</summary>
    public Task AttemptAsync(string runId, string status, string? cancelReason = null, params object[] rows) =>
        AddAsync([TicketStoreHarness.Run(runId, status: status, cancelReason: cancelReason), TicketStoreHarness.Step(runId), .. rows]);

    public async Task AddAsync(params object[] rows)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        foreach (var row in rows) ctx.Add(row);
        await ctx.SaveChangesAsync();
    }

    /// <summary>POST /api/runs/{runId}/retry as <paramref name="principal"/>.</summary>
    public async Task<IResult> RetryAsync(string runId, string principal = Principal)
    {
        await using var scope = _provider.CreateAsyncScope();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", principal)], "test")),
            RequestServices = scope.ServiceProvider,
        };
        return await RetryEndpoints.RetryAsync(runId, http, scope.ServiceProvider.GetRequiredService<RunRetry>(), default);
    }

    /// <summary>2026-10-14-da5ed: a Retry asked in the ticket's conversation.</summary>
    public Task<RunRetryResult> ConversationRetryAsync(string runId, string principal = Principal) =>
        InScopeAsync(sp => sp.GetRequiredService<RunRetry>().RetryAsync(runId, principal, RetryOrigin.Conversation, default));

    /// <summary>2026-10-14-da5ed: the start port a bound turn of conversation "s-1" on ticket 42 is seeded with.</summary>
    public Task<IAttemptStart?> StartPortAsync(TurnPoster poster) =>
        InScopeAsync(sp => sp.GetRequiredService<BoundAttemptStarts>().ForAsync(Project, "42", "s-1", poster, default));

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> act)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await act(scope.ServiceProvider);
    }

    public async Task<List<ReworkNudge>> NudgesAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<ReworkNudge>().AsNoTracking().ToListAsync();
    }

    public async Task<List<RunRetryRequest>> RequestsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunRetryRequest>().AsNoTracking().ToListAsync();
    }

    /// <summary>One worker pass: claim, handle, finish — what ReworkNudgeWorker does with a claimed row.</summary>
    public async Task<ReworkNudgeDisposition> HandleNextAsync()
    {
        var claimed = (await InScopeAsync(sp => sp.GetRequiredService<ReworkNudgeRepository>().ClaimDueAsync(10, default))).Single();
        var disposition = await Handler().HandleAsync(Project, claimed, default);
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ReworkNudgeRepository>().FinishAsync(claimed, default);
            return true;
        });
        return disposition;
    }

    /// <summary>The started run's FetchTicket: its ticket and its previous attempt, as the run sets them.</summary>
    public async Task<AttemptCause> CauseOfNextAttemptAsync(string runId, ReworkAct? keywordAct = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("42"), "Widget drops", "d", null, "open", "github"));
        pipeline.Set(ContextKeys.TrackerConnection, "gh");
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.CodeName);
        if (keywordAct is not null) pipeline.Set(ContextKeys.ReworkAct, keywordAct);
        if (await _provider.GetRequiredService<IPreviousAttemptReader>().LatestAsync("alpha", "42", runId, default) is { } previous)
            pipeline.Set(ContextKeys.PreviousAttempt, previous);
        var reader = new AttemptCauseReader(new TrackerIdentity(Tracker, NullLogger<TrackerIdentity>.Instance),
            new ApprovedSpecSetResolver(Approvals, NullLogger<ApprovedSpecSetResolver>.Instance), NullLogger<AttemptCauseReader>.Instance);
        return await reader.ReadAsync(Tracker.Create(new TrackerConnection()),
            new FetchTicketContext(new TicketId("42"), Project.Tracker, pipeline, null, null), default);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private ReworkNudgeHandler Handler()
    {
        var sp = _provider;
        var scopes = sp.GetRequiredService<IServiceScopeFactory>();
        var leases = sp.GetRequiredService<IActiveRunLease>();
        var attempts = sp.GetRequiredService<IPreviousAttemptReader>();
        var speech = new ReworkSpeech(scopes, Tracker, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), Tracker,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        var pending = new ReworkPendingActs(Tracker, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), Approvals);
        var entry = new ReworkEntry(leases, attempts, new ReworkParkCheck(Tracker, Mock.Of<IUnmovedTicketStore>(), sp.GetRequiredService<TicketClosure>()),
            new TicketReopener(new InMemorySpecSetPointerStore(), Approvals, Mock.Of<IUnmovedTicketStore>()), Launch.Object,
            Mock.Of<IWaitingRunCloser>(), NullLogger<ReworkEntry>.Instance);
        return ReworkHandlers.Of(leases, attempts, pending, entry, speech, fallback,
            new ReworkWithheld(scopes, speech), sp.GetRequiredService<IRunCheckpointStore>(), ClosedTicketFollowUps.Of(scopes, Tracker, speech));
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
