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

using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-20-4b0af: the subject a conversation is headed with — 2026-11-04-96a0a: named by the master through
/// name_conversation and stored by the router between persisting the turn and sending the reply that makes the page
/// read the conversation again, and served on the session view that read returns. 2026-11-05-862ea: the work picture
/// state_work_picture stated is stored the same way, over a stored one, and served on the same view.
/// </summary>
public sealed class ConversationSubjectTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Channel = "C1";
    private const string Thread = "th-4b0af";
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

    /// <summary>What happened, in the order it happened: the reply and the digest.</summary>
    private readonly List<string> _order = [];

    /// <summary>The states the turn runner was handed, in order.</summary>
    private readonly List<ConversationState> _ranOn = [];

    private string _answer = Minted;

    /// <summary>What the master named the conversation with through name_conversation, or null.</summary>
    private string? _named;

    /// <summary>The stored subject as it stood when the reply was sent.</summary>
    private string? _subjectAtReply;

    /// <summary>What the master stated through state_work_picture, or null.</summary>
    private WorkPicture? _pictured;

    /// <summary>The stored work picture's JSON as it stood when the reply was sent.</summary>
    private string? _pictureAtReply;

    /// <summary>What the design turn answers.</summary>
    private string _reply = "canned design answer";

    /// <summary>What the turn runner reports; null lets the outcome decide, as it normally does.</summary>
    private SpecDialogTurnKind? _turnKind;

    public ConversationSubjectTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        _adapter.Setup(a => a.SendInfoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                _order.Add("reply");
                _subjectAtReply = _context.SpecDialogSessions.AsNoTracking().Select(row => row.Subject).Single();
                _pictureAtReply = _context.SpecDialogSessions.AsNoTracking().Select(row => row.WorkPictureJson).Single();
            })
            .Returns(Task.CompletedTask);

        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(_repository), AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        _chat = new RecordingChatClients(() => _answer);
        _router = Router();
    }

    [Fact]
    public async Task SpecDialogRouter_ATurnThatNamedTheConversation_StoresTheSubjectBeforeItSendsTheReply()
    {
        await OpenAsync();
        _named = Minted;

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().Be(Minted);
        _subjectAtReply.Should().Be(Minted,
            "a subject stored after the push that makes the page re-read the conversation "
            + "would be a read too late for a conversation that never comes back");
        _chat.Prompts.Should().BeEmpty("the master names the conversation; no summarization call is made for it");
    }

    // A notice is in the master's language too, so what the master named on it is kept.
    [Fact]
    public async Task SpecDialogRouter_ANoticeTurnThatNamedTheConversation_StoresTheSubject()
    {
        await OpenAsync();
        _named = Minted;
        _turnKind = SpecDialogTurnKind.Notice;

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().Be(Minted);
    }

    // A failed turn is half an exchange; a stored subject is never revised, so a heading named after one would stand.
    [Fact]
    public async Task SpecDialogRouter_AFailedTurn_StoresNoSubject()
    {
        await OpenAsync();
        _named = Minted;
        _turnKind = SpecDialogTurnKind.Failure;

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SpecDialogRouter_AConversationWithASubject_KeepsIt()
    {
        await OpenAsync();
        await SetSubjectAsync("a subject named somewhere else");
        _named = "a second subject nobody asked for";

        await SendAsync("a widget that reads the ledger");

        (await SubjectAsync()).Should().Be("a subject named somewhere else");
    }

    [Fact]
    public async Task SpecDialogRouter_ATurnThatNamedNothing_StoresNothingAndTheNextTurnIsNameable()
    {
        await OpenAsync();
        await SendAsync("a widget that reads the ledger");

        await SendAsync("and it should page the results");

        (await SubjectAsync()).Should().BeNull();
        _ranOn.Should().HaveCount(2);
        _ranOn[^1].Subject.Should().BeNull("the next turn is seeded as nameable and offered name_conversation again");
    }

    // 2026-10-14-dbceo: the router keeps the reply and the subject, and folds a conversation past its window only
    // once the turn gate is released, so a message typed while the digest is written starts its own turn.
    [Fact]
    public async Task SpecDialogRouter_KeptReplyPastTheBound_WritesTheDigestAfterTheGateIsReleased()
    {
        var state = await OpenAsync();
        await SetSubjectAsync(Minted);
        var row = await Row();
        row.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. Enumerable.Range(0, 8).Select(i => new TranscriptTurn(
            i % 2 == 0 ? TranscriptRole.User : TranscriptRole.Assistant, $"T{i} " + new string('x', 9_000), DateTimeOffset.UtcNow,
            i % 2 == 0 ? null : SpecDialogTurnKind.Answer))]);
        await _repository.SaveAsync(CancellationToken.None);
        bool? gateFree = null;
        _answer = "the digest of the start";
        _chat.OnCall = () =>
        {
            gateFree = _turnGate.TryEnter(state.JobId);
            if (gateFree == true) _turnGate.Exit(state.JobId);
            _order.Add("digest");
        };

        await SendAsync("one more question");

        gateFree.Should().BeTrue("the digest is written after the turn gate is released");
        _order.Should().Equal(["reply", "digest"]);
        var transcript = SpecDialogSessionMapper.ReadTranscript((await Row()).TranscriptJson);
        transcript.Should().HaveCount(11);
        transcript[^1].Kind.Should().Be(SpecDialogTurnKind.Digest);
        transcript[^1].Text.Should().EndWith("the digest of the start");
        _chat.Ceilings.Should().Equal(4000);
    }

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

    private static readonly WorkPicture Rebuild =
        new(UndertakingKind.Rebuild, "the handover upload; repo-a is scaffolding", UndertakingSize.Series);

    [Fact]
    public async Task SpecDialogRouter_TurnThatStatedAWorkPicture_StoresItBeforeTheReplyIsSent()
    {
        await OpenAsync();
        _pictured = Rebuild;

        await SendAsync("rebuild the legacy app from the upload");

        SpecDialogSessionMapper.ReadWorkPicture((await Row()).WorkPictureJson).Should().Be(Rebuild);
        SpecDialogSessionMapper.ReadWorkPicture(_pictureAtReply).Should().Be(Rebuild,
            "the reply makes the page re-read the conversation, so the picture is stored first");
    }

    [Fact]
    public async Task SpecDialogRouter_TurnCorrectingTheWorkPicture_OverwritesIt()
    {
        await OpenAsync();
        _pictured = Rebuild;
        await SendAsync("rebuild the legacy app from the upload");
        var corrected = Rebuild with { Kind = UndertakingKind.Feature, Size = UndertakingSize.OneSpec };
        _pictured = corrected;

        await SendAsync("no, it is one feature in repo-a");

        SpecDialogSessionMapper.ReadWorkPicture((await Row()).WorkPictureJson).Should().Be(corrected);
        _ranOn[^1].WorkPicture.Should().Be(Rebuild, "the second turn was seeded from the stored picture");
    }

    [Fact]
    public async Task TurnKeeper_LowerEffortLater_KeepsTheHigherTier() // 2026-11-10-548f: the higher tier wins
    {
        await OpenAsync();
        _pictured = Rebuild with { Effort = ComplexityTier.Xl };
        await SendAsync("rebuild the legacy app from the upload");
        _pictured = Rebuild with { Effort = ComplexityTier.Small };

        await SendAsync("start with the login page");

        SpecDialogSessionMapper.ReadWorkPicture((await Row()).WorkPictureJson)!.Effort.Should().Be(ComplexityTier.Xl);
    }

    [Fact]
    public async Task TurnKeeper_FailedTurnWithPicture_StoresIt() // 2026-11-10-548f: a failed turn keeps what it stated
    {
        await OpenAsync();
        _pictured = Rebuild;
        _turnKind = SpecDialogTurnKind.Failure;

        await SendAsync("rebuild the legacy app from the upload");

        (await Row()).WorkPictureJson.Should().Contain("rebuild");
    }

    [Fact]
    public async Task SpecDialogViewReader_StoredWorkPicture_ReachesTheView()
    {
        await OpenAsync();
        (await Row()).WorkPictureJson = SpecDialogSessionMapper.WriteWorkPicture(Rebuild);
        await _repository.SaveAsync(CancellationToken.None);

        var view = await Reader().ReadAsync(Thread, CancellationToken.None);

        view.Session!.WorkPicture.Should().Be(new WorkPictureView("rebuild", Rebuild.Truth, "series"));
    }

    private Task<ConversationState> OpenAsync() =>
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
        new AgentSmith.Tests.TestSupport.SpecDialogDecisionHarness(_context).View(_sessions, new SpecDialogProjectCatalog(Loader()), new SpecDialogPendingQuestions(_turnGate), new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()), _turnGate, new SpecDialogTicketTextRepository(_context), TestUploads.Over(_context));

    private SpecDialogRouter Router()
    {
        var composer = new SpecDialogReplyComposer();
        var messenger = new SpecDialogMessenger(
            [_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var pendingQuestions = new SpecDialogPendingQuestions(_turnGate);
        var turnRunner = new Mock<ISpecDialogTurnRunner>();
        turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .Callback((ConversationState state, TurnPoster _, CancellationToken _) => _ranOn.Add(state))
            .ReturnsAsync(() => SpecDialogTurnResult.On(Platform, _reply, new AnswerOutcome(), _turnKind, subject: _named, workPicture: _pictured));
        var outcomeComposer = new SpecDialogOutcomeComposer();
        var outcomeFlowHarness = new AgentSmith.Tests.TestSupport.SpecDialogDecisionHarness(_context, null, Mock.Of<IOutcomeSink>(), messenger);
        var outcomeFlow = outcomeFlowHarness.Flow();
        return outcomeFlowHarness.Router(new SpecDialogCommandHandler(
                _sessions, new SpecDialogScopeResolver(Loader()), composer, messenger), turnRunner.Object, _turnGate, new SpecDialogAnswerAdmission(
                _sessions, pendingQuestions,
                Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>()), new SpecDialogTurnKeeper(_sessions, new SpecDialogSubjectStore(_repository),
                new SpecDialogDigestWriter(
                    _chat, Loader(), new ServerContext("agentsmith.yml"), _repository,
                    AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(_repository), TimeProvider.System,
                    NullLogger<SpecDialogDigestWriter>.Instance),
                new SpecDialogUploadArrivals(new ReferenceSetRepository(_context)),
                AgentSmith.Tests.TestSupport.DialogThreadStores.Over(_context),
                new SpecDialogWorkPictureStore(_repository, NullLogger<SpecDialogWorkPictureStore>.Instance)));
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
                    // The digest must reach for THIS, not for a bare fabricated config.
                    Agent = new AgentConfig { Type = "claude", Model = ProjectModel },
                },
            },
        });
        return loader.Object;
    }

    /// <summary>
    /// The chat factory the digest writer is given, which hands out itself: what was asked for, of
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
