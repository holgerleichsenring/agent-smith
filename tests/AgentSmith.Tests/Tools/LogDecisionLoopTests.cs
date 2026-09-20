using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// The tool is not the phase — the LOOP is. Run 2026-09-17T14-41-51-a3e2 proved both halves of that
/// at once: log_decision wrote to the host's /work and threw, and because
/// FunctionInvokingChatClient tolerates three consecutive tool errors and rethrows the fourth
/// (Microsoft.Extensions.AI, MaximumConsecutiveErrorsPerRequest, default 3), the master survived
/// three rewordings of one decision and then died with the run's work already done.
/// <para>
/// These tests run the REAL invocation middleware over the REAL tool: 2026-09-19-c511a that the
/// write reaches the sandbox, 2026-09-19-c511b that four failures in a row do not end the loop.
/// </para>
/// </summary>
public sealed class LogDecisionLoopTests
{
    private const string RunId = "2026-09-17T14-41-51-a3e2";
    private const string Answer = "the step after the decisions";

    private readonly AsyncLocalRunContextAccessor _runContext = new();

    [Fact]
    public async Task MasterLoop_LogDecision_WritesIntoTheRunsSandboxTree()
    {
        var sandbox = new RecordingSandbox();
        var host = new LogDecisionToolHost(
            Logger(), new SandboxFileReaderFactory().Create(sandbox));
        using var scope = _runContext.BeginScope(RunId);

        var answer = await RunLoopAsync(host, calls: 1);

        answer.Should().Be(Answer);
        sandbox.Written.Should().ContainSingle().Which.Should()
            .Be($".agentsmith/decisions/{RunId}.yaml",
                "the repository of a run lives in its sandbox, and that is where its decisions go");
    }

    [Fact]
    public async Task MasterLoop_LogDecisionWhoseWriteFails_ReachesTheStepAfterIt()
    {
        using var scope = _runContext.BeginScope(RunId);

        var answer = await RunLoopAsync(HostOverADeadSandbox(), calls: 1);

        answer.Should().Be(Answer);
    }

    /// <summary>
    /// Four, because three is what the framework tolerates
    /// (FunctionInvokingChatClient.MaximumConsecutiveErrorsPerRequest) and the fourth is the one
    /// that ended the production run. With the sink fixed the tool throws zero times, so this is a
    /// regression guard rather than a proof of the limit: it fails the moment the sink is allowed
    /// to throw again, at the count that actually killed a run.
    /// </summary>
    [Fact]
    public async Task MasterLoop_FourFailingLogDecisions_StillReachTheStepAfterThem()
    {
        using var scope = _runContext.BeginScope(RunId);

        var answer = await RunLoopAsync(HostOverADeadSandbox(), calls: 4);

        answer.Should().Be(Answer);
    }

    private LogDecisionToolHost HostOverADeadSandbox() =>
        new(Logger(), new SandboxFileReaderFactory().Create(new RecordingSandbox(exitCode: 1)));

    private RepositoryDecisionLogger Logger() =>
        new(_runContext,
            new DecisionEventMirror(EventTestStubs.NoOp, _runContext),
            NullLogger<RepositoryDecisionLogger>.Instance);

    /// <summary>The real FunctionInvokingChatClient over the real tool, with its real defaults.</summary>
    private static async Task<string> RunLoopAsync(LogDecisionToolHost host, int calls)
    {
        var tools = host.GetTools(null, null).Cast<AITool>().ToList();
        using var chat = new ChatClientBuilder(new DecidingChatClient(calls))
            .UseFunctionInvocation()
            .Build();

        var response = await chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "do the work")],
            new ChatOptions { Tools = tools },
            CancellationToken.None);
        return response.Text ?? string.Empty;
    }

    /// <summary>Calls log_decision <c>calls</c> times, then answers — the shape the provider produces.</summary>
    private sealed class DecidingChatClient(int calls) : IChatClient
    {
        private int _turn;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            if (_turn++ < calls)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent($"call-{_turn}", "log_decision",
                        new Dictionary<string, object?>
                        {
                            ["category"] = "Implementation",
                            ["decision"] = $"deferred the import, attempt {_turn}",
                        }),
                ])));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>A sandbox that records the paths written to it, or refuses everything.</summary>
    private sealed class RecordingSandbox(int exitCode = 0) : ISandbox
    {
        public string JobId => "loop";
        public List<string> Written { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            if (exitCode == 0 && step.Kind == StepKind.WriteFile) Written.Add(step.Path!);
            // ListFiles answers an EMPTY listing with exit 0 — the shape of a checkout whose
            // decisions directory exists and holds nothing yet, so the listing path is really
            // taken. A dead sandbox refuses every kind alike.
            if (exitCode != 0)
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, "dead"));
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                OutputContent: step.Kind == StepKind.ListFiles ? "[]" : null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
