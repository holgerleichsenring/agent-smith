using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0385: a capped/prose exploration attempt is finalized by continuing the
/// SAME conversation with an emit-JSON-now turn (tools disabled) — never a
/// blank restart that deterministically hits the same iteration cap again.
/// </summary>
public sealed class ProjectAnalyzerTests
{
    private const string ValidJson =
        """{"primary_language": "csharp", "frameworks": ["aspnetcore"]}""";

    private const string Narration =
        "Step 1: listing the root directory. Step 2: reading the build files. Step 3: ...";

    [Fact]
    public async Task AnalyzeAsync_CappedNarrationThenFinalizeJson_ReturnsMap()
    {
        var factory = new RecordingChatClientFactory(Narration, ValidJson);

        var map = await AnalyzeAsync(factory);

        map.PrimaryLanguage.Should().Be("csharp");
        factory.Calls.Should().HaveCount(2, "one exploration attempt plus one finalize turn");
    }

    [Fact]
    public async Task AnalyzeAsync_FinalizeTurn_CarriesPriorMessages_NotBlankRestart()
    {
        var factory = new RecordingChatClientFactory(Narration, ValidJson);

        await AnalyzeAsync(factory);

        var finalize = factory.Calls[1];
        finalize.Should().HaveCount(factory.Calls[0].Count + 2,
            "the finalize turn appends the exploration reply plus one user instruction");
        finalize.Should().Contain(m => m.Role == ChatRole.Assistant && m.Text == Narration,
            "the gathered evidence stays in the conversation");
        finalize[^1].Role.Should().Be(ChatRole.User);
        finalize[^1].Text.Should().Contain("ONLY the JSON object");
        var options = factory.OptionsPerCall[1]!;
        options.ToolMode.Should().Be(ChatToolMode.None, "no further exploration on the finalize turn");
        options.Tools.Should().NotBeNullOrEmpty("tool-bearing history requires the tools to stay declared");
        factory.MaxIterationsPassed.Should().Be(25);
        factory.Calls[0][^1].Text.Should().Contain("budget of 25 tool calls",
            "prompt hint and enforced cap share one constant");
    }

    [Fact]
    public async Task AnalyzeAsync_FirstReplyValidJson_NoFinalizeCall()
    {
        var factory = new RecordingChatClientFactory(ValidJson);

        var map = await AnalyzeAsync(factory);

        map.PrimaryLanguage.Should().Be("csharp");
        factory.Calls.Should().HaveCount(1, "a clean terminal JSON needs no finalize turn");
    }

    [Fact]
    public async Task AnalyzeAsync_FinalizeAlsoProse_SecondAttemptRuns_ThenThrows()
    {
        var factory = new RecordingChatClientFactory(Narration, Narration, Narration, Narration);

        var act = () => AnalyzeAsync(factory);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*2 attempts*finalize*");
        factory.Calls.Should().HaveCount(4, "2 exploration attempts, each with one finalize turn");
        factory.Calls[2].Should().HaveCount(2,
            "the second attempt restarts fresh (system + user) after the first finalize failed");
    }

    private static Task<ProjectMap> AnalyzeAsync(RecordingChatClientFactory factory)
    {
        var analyzer = new ProjectAnalyzer(
            factory, new StubPromptCatalog(), new ProjectMapJsonReader(),
            EventTestStubs.RunContext, new AgentSmith.Application.Services.Tools.AgenticToolSurface(), NullLogger<ProjectAnalyzer>.Instance);
        return analyzer.AnalyzeAsync(
            "/work/repo", new AgentConfig { Type = "claude" }, new StubSandbox(),
            CancellationToken.None);
    }

    /// <summary>
    /// Scripted IChatClientFactory returning canned response texts in order and
    /// recording, per call, a snapshot of the message list and the ChatOptions.
    /// </summary>
    private sealed class RecordingChatClientFactory(params string[] responses) : IChatClientFactory
    {
        private readonly Queue<string> _responses = new(responses);

        public List<IReadOnlyList<ChatMessage>> Calls { get; } = new();
        public List<ChatOptions?> OptionsPerCall { get; } = new();
        public int? MaxIterationsPassed { get; private set; }

        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null)
        {
            MaxIterationsPassed = maxIterations;
            return new Inner(this);
        }

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

        private sealed class Inner(RecordingChatClientFactory owner) : IChatClient
        {
            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                owner.Calls.Add(messages.ToList());
                owner.OptionsPerCall.Add(options);
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, owner._responses.Dequeue())));
            }

            public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages, ChatOptions? options = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
            public void Dispose() { }
        }
    }
}
