using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: a ticket's conversation, its runs and its approval record over a migrated SQLite
/// store, with the continuation wired by its real registration; the tracker is the only substitute.
/// Ticket 42 lives on tracker "gh", shared by projects alpha and beta.
/// </summary>
internal sealed class ContinuationHarness : IDisposable
{
    public const string TicketId = "42";
    public const string Key = "github-42";
    public const string SessionId = "s-1";
    public const string Thread = "d-1";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public ContinuationHarness()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Config).AddSingleton<ITicketProviderFactory>(Tracker).AddSingleton(Activity.Object);
        services.AddSingleton<ISpecApprovalStore>(Approvals).AddSingleton<ISpecSetPointerStore>(Pointers);
        services.AddSingleton<TrackerIdentity>().AddTransient<TicketClosure>().AddTransient<FiledWorkTrackerProjects>();
        services.AddScoped<SpecDialogSessionRepository>().AddScoped<SpecDialogTicketTextRepository>();
        services.AddScoped<ActiveRunRepository>().AddSingleton<IUniqueViolationTranslator, SqliteUniqueViolationTranslator>();
        services.AddSingleton(TimeProvider.System).AddSingleton(Holds());
        services.AddScoped(_ => ApprovedSetDoubles.Recorder(Approvals));
        services.AddSingleton(ApprovedSetDoubles.Branch(Sources, Pointers)).AddSingleton(ApprovedSetDoubles.SeriesFiling());
        services.AddTransient<SpecTicketRenderer>().AddTransient<BugTicketRenderer>().AddTransient<EpicChildOrderer>();
        services.AddScoped(sp => new OutcomeTicketFiler(Config, Tracker, new SpecTicketRenderer(), new BugTicketRenderer(),
            new EpicChildOrderer(), ApprovedSetDoubles.SeriesFiling(), ApprovedSetDoubles.SetFiler(Approvals, sources: Sources, pointers: Pointers),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance));
        services.AddScoped<BoundSessionNudge>().AddScoped<SpecDialogSessionManager>();
        services.AddScoped<SpecDialogOutcomeStore>().AddScoped<SpecDialogLatestOutcomeStore>();
        services.AddSingleton(new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance));
        services.AddTransient<SpecDialogOutcomeComposer>().AddTransient<SpecDialogProposalComposer>();
        services.AddSingleton(sp => new DashboardOutcomeChannel(
            sp.GetRequiredService<SpecDialogProposalComposer>(), NullLogger<DashboardOutcomeChannel>.Instance));
        services.AddScoped<TicketFilingOutcomeSink>();
        Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Config);
        services.AddSingleton(Loader.Object).AddSingleton(new ServerContext("config.yml"));
        services.AddTransient<TicketProjects>().AddTransient<TicketTranscriptReader>();
        services.AddScoped<TicketAmendment>().AddTicketContinuation();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public FakeTicketTracker Tracker { get; } = new();
    public InMemorySpecApprovalStore Approvals { get; } = new();
    public InMemorySpecSetPointerStore Pointers { get; } = new();
    public RecordingBranchSources Sources { get; } = new();
    public Mock<ITicketActivityNudge> Activity { get; } = new();
    public Mock<IConfigurationLoader> Loader { get; } = new();

    public AgentSmithConfig Config { get; } = new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", ["api", "web"]),
            ["beta"] = Project("beta", ["api"]),
        },
    };

    public static ResolvedProject Project(string name, IReadOnlyList<string> repos) => new()
    {
        Name = name,
        DefaultPipeline = "code",
        Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        GithubTrigger = new WebhookTriggerConfig { DoneStatus = "In Review", FailedStatus = "Failed" },
        Repos = [.. repos.Select(r => new RepoConnection { Name = r, DefaultBranch = "main" })],
    };

    public static ConversationState State() => new()
    {
        JobId = SessionId, ChannelId = "c", ThreadId = Thread, UserId = "owner", Platform = "dashboard",
        Project = "alpha", TicketId = string.Empty, Tracker = "gh", TicketKey = Key,
        StartedAt = TicketStoreHarness.T, Mode = ConversationMode.SpecDialog,
        Scope = new ActiveScope { Project = "alpha", Repos = ["api"] },
    };

    /// <summary>The bound conversation with its open proposal and the ticket text it recorded.</summary>
    public async Task BindAsync(OutcomeProposal? proposal)
    {
        await AddAsync(TicketConversationFixtures.Session(proposal), new SpecDialogTicketText
        {
            SessionId = SessionId, TicketId = TicketId, Title = "Widget drops", Text = "Title: Widget drops",
            Fingerprint = "fp", ReadAt = TicketStoreHarness.T,
        });
    }

    /// <summary>A worked attempt that published a cut of <paramref name="specs"/>, with the pointer naming what ran.</summary>
    public async Task WorkedAsync(string runId, string project, string series, IReadOnlyList<string> specs,
        IReadOnlyList<string> executed, string carrier = "web")
    {
        await AddAsync(TicketStoreHarness.Run(runId, project: project), TicketStoreHarness.Step(runId),
            TicketSeriesFixtures.Cut(runId, 1, [.. specs.Select((id, i) => TicketSeriesFixtures.Entry(id, i + 1))]),
            new RunAttempt { RunId = runId, SeriesId = series });
        await Pointers.SaveAsync(project, new SpecSetPointer(Key, carrier, "sha", 1, SeriesId: series,
            RevisionShas: new Dictionary<string, string> { [carrier] = "sha" }, ExecutedIds: executed), default);
    }

    public async Task AddAsync(params object[] rows)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        foreach (var row in rows) ctx.Add(row);
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> OpenProposalJsonAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return (await ctx.Set<SpecDialogSession>().SingleAsync(s => s.SessionId == SessionId)).LatestProposalJson;
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> act)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await act(scope.ServiceProvider);
    }

    public Task AcceptAsync(OutcomeProposal proposal) => InScopeAsync(async sp =>
    {
        await sp.GetRequiredService<TicketFilingOutcomeSink>().AcceptAsync(State(), proposal, false, default);
        return true;
    });

    public Task<TicketContinuationOutcome> ActAsync(OutcomeProposal proposal) =>
        InScopeAsync(sp => sp.GetRequiredService<TicketContinuationAct>().ApplyAsync(State(), proposal, false, default));

    public Task<TicketApprovalPreviewRead?> PreviewAsync() =>
        InScopeAsync(sp => sp.GetRequiredService<TicketApprovalPreview>().ReadAsync("alpha", TicketId, default));

    public Task<SpecApprovalRecord?> RecordAsync() => Approvals.GetAsync("gh", Key, default);

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private static Contracts.Sandbox.IHeldSandboxRegister Holds() => AgentSmith.Tests.Sandbox.Holds.None();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
