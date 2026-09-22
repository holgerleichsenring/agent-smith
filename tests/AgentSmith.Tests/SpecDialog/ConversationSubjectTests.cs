using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-20-4b0af: the subject a conversation is headed with — minted once by the router,
/// between persisting the first assistant turn and sending the reply that makes the page read
/// the conversation again, and served on the session view that read returns.
/// </summary>
public sealed class ConversationSubjectTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Channel = "C1";
    private const string Thread = "th-4b0af";
    private const string Reply = "canned design answer";
    private const string Minted = "Das Widget, das das Hauptbuch liest";
    private const string ProjectModel = "the-project-own-model";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogRouter _router;
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly RecordingChatClients _chat;
    private readonly SpecDialogTurnGate _turnGate = new(TimeProvider.System);

    /// <summary>What happened, in the order it happened: the mint and the reply.</summary>
    private readonly List<string> _order = [];

    private string _answer = Minted;

    /// <summary>What the turn runner reports; null lets the outcome decide, as it normally does.</summary>
    private SpecDialogTurnKind? _turnKind;

    public ConversationSubjectTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        _adapter.Setup(a => a.SendInfoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("reply"))
            .Returns(Task.CompletedTask);

        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _chat = new RecordingChatClients(() => _answer) { OnCall = () => _order.Add("mint") };
        _router = Router();
    }

    [Fact]
    public async Task SpecDialogRouter_AFirstTurn_MintsASubjectBeforeItSendsTheReply()
    {
        await OpenAsync();

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().Be(Minted);
        _order.Should().Equal(["mint", "reply"],
            "a subject stored after the push that makes the page re-read the conversation "
            + "would be a read too late for a conversation that never comes back");
        _chat.Tasks.Should().Equal(TaskType.Summarization);
        _chat.Ceilings.Should().ContainSingle().Which.Should().Be(128);
        _chat.AgentModels.Should().Equal([ProjectModel],
            "the mint runs on the project's own agent, not on a fabricated one");
        _chat.Prompts.Should().ContainSingle()
            .Which.Should().Contain("SAME LANGUAGE").And.Contain("a widget that reads the ledger");
    }

    // The first mint here is REFUSED, so the row still has no subject when the second turn runs:
    // what stops the second turn is the conversation's own first assistant turn, and nothing
    // else. A conversation whose mint was refused must not pay for another one every turn.
    [Fact]
    public async Task SpecDialogRouter_ALaterTurn_MintsNothing()
    {
        await OpenAsync();
        _answer = "```\nnot one line of prose\n```";
        await SendAsync("a widget that reads the ledger");
        _answer = Minted;

        await SendAsync("and it should page the results");

        _chat.Prompts.Should().ContainSingle(
            "the subject is minted once, and a refused mint is not retried turn after turn");
        (await SubjectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SpecDialogRouter_ASecondTurnOfAMintedConversation_MintsNothing()
    {
        await OpenAsync();
        await SendAsync("a widget that reads the ledger");
        _answer = "a second subject nobody asked for";

        await SendAsync("and it should page the results");

        _chat.Prompts.Should().ContainSingle("a subject already minted is never revised");
        (await SubjectAsync()).Should().Be(Minted);
    }

    // A failed turn is half an exchange. Because the subject is never re-minted, a heading named
    // after one would stand over the conversation for good.
    [Fact]
    public async Task SpecDialogRouter_AFailedTurn_MintsNothing()
    {
        await OpenAsync();
        _turnKind = SpecDialogTurnKind.Failure;

        await SendAsync("a widget that reads the ledger");

        _chat.Prompts.Should().BeEmpty("a failed turn names nothing, and nothing would rename it");
        (await SubjectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SpecDialogRouter_AConversationThatAlreadyHasASubject_MintsNothing()
    {
        await OpenAsync();
        await SetSubjectAsync("a subject minted somewhere else");

        await SendAsync("a widget that reads the ledger");

        _chat.Prompts.Should().BeEmpty();
        (await SubjectAsync()).Should().Be("a subject minted somewhere else");
    }

    [Fact]
    public async Task SpecDialogRouter_AMinterThatThrows_StillPersistsTheTurnAndSendsTheReply()
    {
        await OpenAsync();
        _chat.OnCall = () => throw new TimeoutException("the model did not answer");

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().BeNull("a refused mint stores nothing");
        var state = await _sessions.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None);
        state!.Transcript.Should().Contain(turn => turn.Role == TranscriptRole.Assistant);
        _order.Should().Equal(["reply"],
            "a heading is not worth the reply the turn already has");
    }

    [Theory]
    [InlineData("A widget that reads the ledger.\nAnd pages the results.")]
    [InlineData("```\nA widget that reads the ledger\n```")]
    [InlineData("**A widget that reads the ledger**")]
    [InlineData("- A widget that reads the ledger")]
    [InlineData("\"A widget that reads the ledger\"")]
    [InlineData("1. A widget that reads the ledger")]
    public void ConversationSubject_AParagraphOrAFencedAnswer_IsDiscarded(string answer) =>
        SpecDialogSubjectAdmission.Of(answer).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t  ")]
    public void ConversationSubject_AnEmptyAnswer_IsDiscarded(string? answer) =>
        SpecDialogSubjectAdmission.Of(answer).Should().BeNull();

    [Fact]
    public void ConversationSubject_AnOverlongAnswer_IsDiscarded()
    {
        SpecDialogSubjectAdmission.Of(new string('a', 121)).Should().BeNull(
            "a subject longer than the column is discarded, not truncated — it is never re-minted");
        SpecDialogSubjectAdmission.Of(new string('a', 120)).Should().Be(new string('a', 120));
    }

    [Fact]
    public void ConversationSubject_OneLineOfProse_IsAdmittedAsWritten() =>
        SpecDialogSubjectAdmission.Of($"  {Minted}  ").Should().Be(Minted);

    [Fact]
    public async Task SpecDialogSessionView_ASessionWithASubject_ServesIt()
    {
        await OpenAsync();
        await SetSubjectAsync(Minted);

        var view = await Reader().ReadAsync(Thread, CancellationToken.None);

        view.Session!.Subject.Should().Be(Minted);
    }

    [Fact]
    public async Task SpecDialogSessionView_ASessionWithout_ServesNone()
    {
        await OpenAsync();

        var view = await Reader().ReadAsync(Thread, CancellationToken.None);

        view.Session!.Subject.Should().BeNull();
    }

    private Task OpenAsync() =>
        _sessions.OpenAsync(Platform, Channel, Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);

    private Task SendAsync(string text) =>
        _router.TryRouteAsync(text, "U1", Channel, Thread, Platform, false, CancellationToken.None);

    private async Task<string?> SubjectAsync() =>
        (await Row()).Subject;

    private async Task SetSubjectAsync(string subject)
    {
        (await Row()).Subject = subject;
        await _repository.SaveAsync(CancellationToken.None);
    }

    private async Task<SpecDialogSession> Row() =>
        (await _repository.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!;

    private SpecDialogViewReader Reader() =>
        new(_sessions, new SpecDialogProjectCatalog(Loader()),
            new SpecDialogPendingQuestions(_turnGate),
            new SpecDialogLatestOutcomeStore(
                _repository, NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
            _turnGate, new SpecDialogAttachmentRepository(_context));

    private SpecDialogRouter Router()
    {
        var composer = new SpecDialogReplyComposer();
        var messenger = new SpecDialogMessenger(
            [_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var pendingQuestions = new SpecDialogPendingQuestions(_turnGate);
        var turnRunner = new Mock<ISpecDialogTurnRunner>();
        turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => SpecDialogTurnResult.On(Platform, Reply, new AnswerOutcome(), _turnKind));
        var outcomeComposer = new SpecDialogOutcomeComposer();
        var outcomeFlow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>(), messenger,
                pendingQuestions, outcomeComposer, NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), outcomeComposer, messenger,
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            new SpecDialogLatestOutcomeStore(
                _repository, NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<SpecDialogOutcomeFlow>.Instance);
        return new SpecDialogRouter(
            new SpecCommandParser(), _sessions,
            new SpecDialogCommandHandler(
                _sessions, new SpecDialogScopeResolver(Loader()), composer, messenger),
            turnRunner.Object, outcomeFlow, _turnGate,
            new SpecDialogAnswerAdmission(
                _sessions, pendingQuestions,
                Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>()),
            new SpecDialogSubjectMinter(
                _chat, Loader(), new ServerContext("agentsmith.yml"), _repository,
                NullLogger<SpecDialogSubjectMinter>.Instance),
            new SpecDialogEditReload(_sessions, NullLogger<SpecDialogEditReload>.Instance),
            composer, messenger, NullLogger<SpecDialogRouter>.Instance);
    }

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new()
                {
                    Name = "sample",
                    Repos = [new RepoConnection { Name = "repo-a" }],
                    // The mint must reach for THIS, not for a bare fabricated config.
                    Agent = new AgentConfig { Type = "claude", Model = ProjectModel },
                },
            },
        });
        return loader.Object;
    }

    /// <summary>
    /// The chat factory the minter is given, which hands out itself: what was asked for, of
    /// which agent, on which task type and under which ceiling.
    /// </summary>
    private sealed class RecordingChatClients(Func<string> answer) : IChatClientFactory, IChatClient
    {
        internal List<TaskType> Tasks { get; } = [];
        internal List<string> AgentModels { get; } = [];
        internal List<int?> Ceilings { get; } = [];
        internal List<string> Prompts { get; } = [];
        internal Action? OnCall { get; set; }

        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null)
        {
            Tasks.Add(task);
            AgentModels.Add(agent.Model ?? string.Empty);
            return this;
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 2048;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            OnCall?.Invoke();
            Ceilings.Add(options?.MaxOutputTokens);
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, answer())));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
