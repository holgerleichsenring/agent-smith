using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: the router over a migrated store with a scripted turn runner, its gate and its inbox — a
/// turn's script sends messages through the same router and calls a tool the way the master's wrapper does.</summary>
internal sealed class MidTurnRig : IDisposable
{
    public const string Platform = "slack";
    public const string Thread = "th-mid";
    public const string Reply = "the turn's reply";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public MidTurnRig()
    {
        _context = MigratedStoreTemplate.Context(_connection);
        Adapter.SetupGet(a => a.Platform).Returns(Platform);
        Harness = new SpecDialogDecisionHarness(_context, null, Mock.Of<IOutcomeSink>(),
            new SpecDialogMessenger([Adapter.Object], NullLogger<SpecDialogMessenger>.Instance));
        Inbox = new SpecDialogOperatorInbox(Gate);
        var repository = new SpecDialogSessionRepository(_context);
        var commands = new SpecDialogCommandHandler(Harness.Sessions,
            new SpecDialogScopeResolver(Mock.Of<IConfigurationLoader>()), new SpecDialogReplyComposer(), Harness.Messenger);
        Router = Harness.Router(commands, Runner.Object, Gate,
            new SpecDialogAnswerAdmission(Harness.Sessions, Pending, Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>(),
                Harness.Proposals),
            SilentTurnKeeper.Over(repository, Harness.Sessions, _context), inbox: Inbox);
    }

    public SpecDialogDecisionHarness Harness { get; }
    public Mock<IPlatformAdapter> Adapter { get; } = new();
    public Mock<ISpecDialogTurnRunner> Runner { get; } = new();
    public SpecDialogTurnGate Gate { get; } = new(TimeProvider.System);
    public SpecDialogPendingQuestions Pending { get; } = new(new SpecDialogTurnGate(TimeProvider.System));
    public SpecDialogOperatorInbox Inbox { get; }
    public SpecDialogRouter Router { get; }
    public List<TurnPoster> Posters { get; } = [];

    public Task<ConversationState> OpenAsync() => Harness.OpenAsync(Platform, Thread);

    public Task<bool> SendAsync(string text, string user = "U1", string? proposalId = null) =>
        Router.TryRouteAsync(text, user, "C1", Thread, Platform, false, CancellationToken.None, proposalId: proposalId);

    /// <summary>Each turn runs the next script, given its state, poster and the inbox as its master reads it.</summary>
    public void Turns(params Func<ConversationState, TurnPoster, IMidTurnMessages, Task>[] scripts)
    {
        var next = 0;
        Runner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConversationState state, TurnPoster poster, CancellationToken _) =>
            {
                Posters.Add(poster);
                var capture = new ForwardedThreadCapture();
                if (next < scripts.Length) await scripts[next++](state, poster, Inbox.For(state.JobId, poster, capture));
                return SpecDialogTurnResult.On(Platform, Reply, new AnswerOutcome(), thread: capture);
            });
    }

    /// <summary>One tool call through the master's wrapper: the tool's own text plus what was handed on.</summary>
    public static async Task<string> CallToolAsync(IMidTurnMessages messages)
    {
        var tool = new MidTurnMessageAIFunction(AIFunctionFactory.Create(() => "file text", "read_file"), messages);
        return (await tool.InvokeAsync(new AIFunctionArguments()))!.ToString()!;
    }

    public int BusyReplies() => Adapter.Invocations.Count(i =>
        i.Arguments.OfType<string>().Any(a => a.Contains("Still working on the previous turn", StringComparison.Ordinal)));

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
