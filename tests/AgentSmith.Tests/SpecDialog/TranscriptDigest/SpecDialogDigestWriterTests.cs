using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>2026-10-14-dbceo: the digest writer folds a conversation past its window into one stored assistant turn.</summary>
public sealed class SpecDialogDigestWriterTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Thread = "th-digest";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly DigestChat _chat = new();
    private readonly SpecDialogDigestWriter _writer;

    public SpecDialogDigestWriterTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(_repository, BoundSessionNudges.Quiet(_repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _writer = new SpecDialogDigestWriter(_chat, Loader(), new ServerContext("agentsmith.yml"), _repository,
            BoundSessionNudges.Quiet(_repository), TimeProvider.System, NullLogger<SpecDialogDigestWriter>.Instance);
    }

    [Fact]
    public async Task SpecDialogDigestWriter_FoldDue_AppendsOneDigestTurnWithDigestThroughAndKeepsEveryTurn()
    {
        var state = await SeedAsync(Long());

        await _writer.WriteAsync(state, CancellationToken.None);

        var transcript = await TranscriptAsync();
        transcript.Should().HaveCount(11);
        transcript.Take(10).Select(t => t.Text).Should().Equal(Long().Select(t => t.Text));
        var digest = transcript[^1];
        digest.Kind.Should().Be(SpecDialogTurnKind.Digest);
        digest.Role.Should().Be(TranscriptRole.Assistant);
        digest.DigestThrough.Should().Be(6);
        digest.Text.Should().Be(SpecDialogDigestInstruction.Heading(0, 6) + "\n\nthe digest");
        _chat.Tasks.Should().Equal(TaskType.Summarization);
        _chat.Ceilings.Should().Equal(4000);
        _chat.Prompts.Single().Should().Contain("T6").And.NotContain("T7 ");
    }

    [Fact]
    public async Task SpecDialogDigestWriter_SummaryCallFails_LeavesTheTranscriptAndTheNextKeepRetries()
    {
        var state = await SeedAsync(Long());
        _chat.OnCall = () => throw new TimeoutException("the model did not answer");

        await _writer.WriteAsync(state, CancellationToken.None);

        (await TranscriptAsync()).Should().HaveCount(10);
        _chat.OnCall = null;
        await _writer.WriteAsync(state, CancellationToken.None);
        (await TranscriptAsync()).Should().HaveCount(11);
    }

    [Fact]
    public async Task SpecDialogDigestWriter_SummaryWithADraftBlock_StoresItStripped()
    {
        var state = await SeedAsync(Long());
        _chat.Answer = "the settled facts\n\n```yaml\nspec: p9001\n```\n";

        await _writer.WriteAsync(state, CancellationToken.None);

        var digest = (await TranscriptAsync())[^1];
        digest.Text.Should().Contain("the settled facts").And.NotContain("```yaml").And.NotContain("p9001");
    }

    [Fact]
    public async Task SpecDialogDigestWriter_FoldedPersonTurns_ReachTheModelWithTheirAuthorAndTheDigestIsAuthoredAgentSmith()
    {
        var state = await SeedAsync([.. Long().Select((t, i) => i == 2 ? t with { Author = "Ada" } : t)]);

        await _writer.WriteAsync(state, CancellationToken.None);

        _chat.Prompts.Single().Should().Contain("[user: Ada] T2").And.Contain("WHO made each");
        (await TranscriptAsync())[^1].Author.Should().Be("agent-smith");
    }

    [Fact]
    public async Task SpecDialogDigestWriter_OperatorTurnAdmittedDuringTheCall_IsKept()
    {
        var state = await SeedAsync(Long());
        _chat.OnCall = () => _sessions.AppendTurnAsync(Platform, Thread, TranscriptRole.User, "typed meanwhile", null, null,
            CancellationToken.None).GetAwaiter().GetResult();

        await _writer.WriteAsync(state, CancellationToken.None);

        var transcript = await TranscriptAsync();
        transcript.Should().HaveCount(12);
        transcript[10].Text.Should().Be("typed meanwhile");
        transcript[11].Kind.Should().Be(SpecDialogTurnKind.Digest);
    }

    [Fact]
    public async Task SpecDialogDigestWriter_SessionForkedDuringTheCall_AppendsNothing()
    {
        var state = await SeedAsync(Long());
        _chat.OnCall = () => _sessions.OpenAsync(Platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None).GetAwaiter().GetResult();

        await _writer.WriteAsync(state, CancellationToken.None);

        (await TranscriptAsync()).Should().BeEmpty("the new conversation on the thread gets no digest of the old one");
        var old = await _context.Set<AgentSmith.Infrastructure.Persistence.Entities.SpecDialogSession>()
            .SingleAsync(s => s.SessionId == state.JobId);
        SpecDialogSessionMapper.ReadTranscript(old.TranscriptJson).Should().HaveCount(10);
    }

    private static List<TranscriptTurn> Long() =>
    [
        .. Enumerable.Range(0, 10).Select(i => new TranscriptTurn(
            i % 2 == 0 ? TranscriptRole.User : TranscriptRole.Assistant, $"T{i} " + new string('x', 9_000),
            DateTimeOffset.UnixEpoch, i % 2 == 0 ? null : SpecDialogTurnKind.Answer)),
    ];

    private async Task<ConversationState> SeedAsync(IReadOnlyList<TranscriptTurn> turns)
    {
        var state = await _sessions.OpenAsync(Platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        var row = (await _repository.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!;
        row.TranscriptJson = SpecDialogSessionMapper.WriteTranscript(turns);
        await _repository.SaveAsync(CancellationToken.None);
        return state;
    }

    private async Task<IReadOnlyList<TranscriptTurn>> TranscriptAsync() =>
        SpecDialogSessionMapper.ReadTranscript(
            (await _repository.GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!.TranscriptJson);

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["sample"] = new() { Name = "sample" } },
        });
        return loader.Object;
    }

    /// <summary>The chat factory the writer is given, which hands out itself and records what it was asked.</summary>
    private sealed class DigestChat : IChatClientFactory, IChatClient
    {
        internal List<TaskType> Tasks { get; } = [];
        internal List<int?> Ceilings { get; } = [];
        internal List<string> Prompts { get; } = [];
        internal Action? OnCall { get; set; }
        internal string Answer { get; set; } = "the digest";

        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null)
        {
            Tasks.Add(task);
            return this;
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 2048;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            OnCall?.Invoke();
            Ceilings.Add(options?.MaxOutputTokens);
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
