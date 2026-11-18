using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.RunMessages;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.RunMessages;

/// <summary>2026-11-17-4860b: the router over a migrated store, a conversation bound to ticket 7 of project 'sample', a
/// lease the test sets, and the server's port the run reads the conversation through.</summary>
internal sealed class TicketMessageRig : IDisposable
{
    public const string Platform = "slack";
    public const string Thread = "th-ticket";
    public const string Project = "sample";
    public const string TicketId = "7";
    public const string Run = "2026-11-17T17-00-00-ab12";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public TicketMessageRig()
    {
        _context = MigratedStoreTemplate.Context(_connection);
        Adapter.SetupGet(a => a.Platform).Returns(Platform);
        Harness = new SpecDialogDecisionHarness(_context, null, Mock.Of<IOutcomeSink>(),
            new SpecDialogMessenger([Adapter.Object], NullLogger<SpecDialogMessenger>.Instance));
        var repository = new SpecDialogSessionRepository(_context);
        var projects = LiveConfig.Projects(Config());
        var admission = new SpecDialogAnswerAdmission(Harness.Sessions,
            new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
            Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>(), Harness.Proposals);
        var divert = new SpecDialogRunDivert(new SpecDialogTicketTextRepository(_context),
            new TicketRunningAttempt(new FiledWorkTrackerProjects(projects), Leases.Object, Attempts.Object), admission,
            repository, BoundSessionNudges.Quiet(repository), Harness.Notices, NullLogger<SpecDialogRunDivert>.Instance, Series);
        var commands = new SpecDialogCommandHandler(Harness.Sessions,
            new SpecDialogScopeResolver(Mock.Of<IConfigurationLoader>()), new SpecDialogReplyComposer(), Harness.Messenger);
        Runner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "the turn's reply", new AnswerOutcome()));
        Router = Harness.Router(commands, Runner.Object, new SpecDialogTurnGate(TimeProvider.System), admission,
            SilentTurnKeeper.Over(repository, Harness.Sessions, _context), runs: divert);
        Port = new TicketConversationMessages(new ServiceCollection()
            .AddSingleton<IUnitOfWork>(_context).AddSingleton(repository).AddSingleton(projects)
            .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    public SpecDialogDecisionHarness Harness { get; }
    public Mock<IPlatformAdapter> Adapter { get; } = new();
    public Mock<ISpecDialogTurnRunner> Runner { get; } = new();
    public Mock<IActiveRunLease> Leases { get; } = new();
    public Mock<IPreviousAttemptReader> Attempts { get; } = new();
    public AgentSmith.Tests.Series.InMemorySeriesRunStore Series { get; } = new(); // 2026-11-17-0207b
    public SpecDialogRouter Router { get; }
    public ITicketConversationMessages Port { get; }

    /// <summary>A conversation on the thread, bound to the ticket the way a ticket's own conversation is.</summary>
    public async Task<ConversationState> OpenBoundAsync()
    {
        var state = await Harness.OpenAsync(Platform, Thread);
        var session = (await Harness.Repository.GetOpenByThreadAsync(Platform, Thread, default))!;
        session.Tracker = "sample-tracker";
        session.TicketKey = "github-7";
        await Harness.Repository.SaveAsync(default);
        await new SpecDialogTicketTextRepository(_context).SaveAsync(new SpecDialogTicketText
        {
            SessionId = state.JobId, TicketId = TicketId, Title = "Export", Text = "Title: Export", Fingerprint = "f",
            ReadAt = DateTimeOffset.UtcNow,
        }, default);
        return state;
    }

    /// <summary>The ticket's lease names <see cref="Run"/>, which is its newest code attempt.</summary>
    public void Running()
    {
        Leases.Setup(l => l.GetByTicketAsync(Project, It.Is<TicketId>(t => t.Value == TicketId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease(Project, new TicketId(TicketId), Run, null));
        Attempts.Setup(a => a.LatestAsync(Project, TicketId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt(Run, "running", DateTimeOffset.UtcNow, false));
    }

    public Task<SpecDialogRouted> SendAsync(string text, bool mayStartRuns = true) =>
        Router.RouteAsync(text, "U1", "C1", Thread, Platform, mayStartRuns, CancellationToken.None);

    public async Task<IReadOnlyList<TranscriptTurn>> TranscriptAsync() =>
        (await Harness.Sessions.GetOpenByThreadAsync(Platform, Thread, default))!.Transcript;

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            [Project] = new()
            {
                Name = Project, DefaultPipeline = "code",
                Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.GitHub },
                Repos = [new RepoConnection { Name = "repo-a", DefaultBranch = "main" }],
            },
        },
    };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
