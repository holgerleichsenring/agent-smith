using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Checks;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ticket readers resolved through the real registration, over a real
/// migrated SQLite store; the capacity queue and the relaunch lease are the only substitutes.
/// </summary>
internal sealed class TicketStoreHarness : IDisposable
{
    public static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-13T09:00:00Z");

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public TicketStoreHarness(Action<IServiceCollection>? configure = null)
    {
        Lease.Setup(l => l.GetByTicketAsync(It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => HeldLease);
        Queue.Setup(q => q.GetPositionsByRunIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Positions);
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunCheckpointRepository>();
        services.AddSingleton<IRunCheckpointStore, DbRunCheckpointStore>();
        services.AddSingleton(Queue.Object).AddSingleton(Lease.Object);
        services.AddSingleton<TimeProvider>(new FixedClock(T.AddHours(3)));
        services.AddSingleton(Config);
        services.AddTransient<FiledWorkSpecReviews>().AddTransient<FiledWorkRunsReader>().AddTransient<TicketRunEndingsReader>();
        services.AddScoped<PrReviewRecordRepository>().AddScoped<NamedPullRequestReader>();
        services.AddScoped<ReworkLedgerRepository>(); // 2026-10-13-8762v: the runway reads the ledger
        services.AddSingleton(Nudges.Object);
        // 2026-10-13-9a59h: the tracker, the configuration loader and the activity nudge are the check's and the
        // conversation's substitutes; the session store and the pull-request read port are real.
        Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Config);
        Trackers.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(() => Tracker.Object);
        services.AddSingleton(Loader.Object).AddSingleton(new ServerContext("config.yml"));
        services.AddSingleton<AgentSmith.Application.Services.Specs.ISeriesSettingsReader,
            AgentSmith.Application.Services.Specs.LoaderSeriesSettingsReader>(); // 2026-10-13-8762u
        services.AddSingleton(Trackers.Object).AddSingleton(Activity.Object);
        services.AddSingleton<TrackerIdentity>();
        services.AddScoped<IPrReviewRecordReader>(sp => sp.GetRequiredService<PrReviewRecordRepository>());
        services.AddScoped<SpecDialogSessionRepository>();
        services.AddSingleton<IUniqueViolationTranslator, SqliteUniqueViolationTranslator>();
        services.AddSingleton<AgentSmith.Contracts.Specs.ISpecApprovalStore>(Approvals); // 2026-10-13-8762l
        services.AddTicketReaders();
        services.AddTicketDefaults(); // 2026-10-13-8762p
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public Mock<ICapacityQueue> Queue { get; } = new();
    public Mock<IActiveRunLease> Lease { get; } = new();
    public Mock<IReworkNudges> Nudges { get; } = new();
    public Mock<IConfigurationLoader> Loader { get; } = new();
    public Mock<ITicketProviderFactory> Trackers { get; } = new();
    public Mock<ITicketProvider> Tracker { get; } = new();
    public Mock<ITicketActivityNudge> Activity { get; } = new();
    public Dictionary<string, int> Positions { get; } = new(StringComparer.Ordinal);
    public AgentSmith.Application.Services.Persistence.InMemorySpecApprovalStore Approvals { get; } = new();
    public StaleLease? HeldLease { get; set; }

    public AgentSmithConfig Config { get; } = new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new() { Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub } },
        },
    };

    public TicketListReader List => _provider.GetRequiredService<TicketListReader>();
    public TicketDetailReader Detail => _provider.GetRequiredService<TicketDetailReader>();
    public TicketSeriesReader Series => _provider.GetRequiredService<TicketSeriesReader>();
    public TicketConversationReader Conversation => _provider.GetRequiredService<TicketConversationReader>();
    public TicketReviewsReader Reviews => _provider.GetRequiredService<TicketReviewsReader>();
    public AgentSmith.Server.Services.Tickets.Defaults.TicketDefaultsReader Defaults =>
        _provider.GetRequiredService<AgentSmith.Server.Services.Tickets.Defaults.TicketDefaultsReader>();
    public AgentSmith.Server.Services.Tickets.Defaults.DefaultVerdictWriter Verdicts =>
        _provider.GetRequiredService<AgentSmith.Server.Services.Tickets.Defaults.DefaultVerdictWriter>();
    public TicketBetweenAttemptsChecker Checker => _provider.GetRequiredService<TicketBetweenAttemptsChecker>();
    public TicketCheckKicks Kicks => _provider.GetRequiredService<TicketCheckKicks>();
    public IServiceProvider Services => _provider;

    public async Task AddAsync(params object[] rows)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        foreach (var row in rows) ctx.Add(row);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<T>> ReadAllAsync<T>() where T : class
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task DeleteRunAsync(string runId)
    {
        await using var ctx = new AgentSmithDbContext(Options());
        await ctx.Set<RunStep>().Where(s => s.RunId == runId).ExecuteDeleteAsync();
        await ctx.Set<Run>().Where(r => r.Id == runId).ExecuteDeleteAsync();
    }

    public static Run Run(
        string id, string ticket = "42", string status = "success", string project = "alpha",
        string pipeline = "code", string? platform = "github", decimal cost = 0m, string? cancelReason = null) =>
        new()
        {
            Id = id, Project = project, Pipeline = pipeline, TicketId = ticket, TicketTitle = $"Ticket {ticket}",
            Platform = platform, Status = status, StartedAt = T, CostTotalUsd = cost, CancelReason = cancelReason,
            FinishedAt = status is "running" or "queued" or "waiting_for_input" ? null : T.AddMinutes(30),
        };

    public static RunStep Step(string runId) =>
        new() { RunId = runId, StepIndex = 0, StepName = "Fetching ticket", Status = "success" };

    public static RunSpec Spec(string runId, string specId, string status) =>
        new() { RunId = runId, SpecId = specId, Ordinal = 1, Title = specId, Status = status, StartedAt = T };

    public static RunCheckpoint Checkpoint(string runId, string ticket = "42") => new()
    {
        RunId = runId, Project = "alpha", TicketId = ticket, Pipeline = "code", DialogueJobId = "j",
        QuestionId = "q-1", AskedAt = T.AddMinutes(10), AnswerDeadlineAt = T.AddHours(4),
        QuestionJson = JsonSerializer.Serialize(new DialogQuestion(
            "q-1", QuestionType.FreeText, "Which branch?", null, null, "main", TimeSpan.FromHours(4))),
        RemainingCommandsJson = "[]", ContextJson = "{}",
    };

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
