using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// 2026-11-17-cb32a: a long tool loop through the compactor and the worker bridge, as a Claude Code worker sees it.
/// The cache reads what the previous call wrote, so a call is cheap only when its blocks extend the previous call's.
/// In the replay of 2026-11-17 the executor folded on 14 of 50 calls because a fold kept three rounds whatever their
/// size; here a fold must land under the target, and many calls must pass before the next one.
/// </summary>
public sealed class CompactionCachePrefixTests
{
    // Window 10,000 tokens: trigger 7,000, target 3,500. A round is about 1,000 tokens (a 4,000-char tool result), so
    // a fold keeps three rounds (about 3,050 tokens with the head) and the next fold comes four rounds later.
    private static readonly CompactionConfig Config = new()
    {
        IsEnabled = true, MaxContextTokens = 10_000, MaxContextTokensTriggerRatio = 0.7,
        FoldTargetRatio = 0.35, SummaryBudgetRatio = 0.1, KeepRecentIterations = 3,
    };

    private const string Segment = "[Context summary of earlier iterations, part ";

    [Fact]
    public async Task Loop_ThirtyCalls_FoldsRarely_AndEveryOtherCallExtendsThePreviousBlocks()
    {
        var runner = new ScriptedWorkerProcessRunner();
        for (var i = 0; i < 30; i++) runner.EnqueueToolCall("read_file", $$"""{"path":"src/F{{i}}.cs"}""");
        runner.EnqueueText("done");
        var foldCalls = new List<int>();
        var ledger = 0;

        await RunLoop(runner, new CompactingChatClient(Bridge(runner), Config,
            new MasterLoopHooks(RenderLedgerForPin: () => $"LEDGER v{++ledger}"), // changes on every render
            (middle, _) =>
            {
                foldCalls.Add(runner.Parts.Count + 1);
                return Task.FromResult($"SUMMARY {foldCalls.Count}: {middle.Count} messages");
            }));

        runner.Parts.Should().HaveCount(31);
        foldCalls.Should().HaveCountGreaterThanOrEqualTo(4, "thirty rounds of 1,000 tokens cross a 7,000 trigger");
        foldCalls.Zip(foldCalls.Skip(1)).Should().OnlyContain(p => p.Second - p.First >= 3,
            "a fold lands under the target, so the next one is several calls away");

        for (var call = 2; call <= runner.Parts.Count; call++)
        {
            var previous = runner.Parts[call - 2].InputBlocks;
            var current = runner.Parts[call - 1].InputBlocks;
            if (!foldCalls.Contains(call))
            {
                current.Take(previous.Count).Should().Equal(previous, $"call {call} extends call {call - 1}");
                continue;
            }
            // A fold rewrites only from its own segment on: the head and the earlier segments are as they were.
            var newest = current.ToList().FindLastIndex(b => b.Contains(Segment));
            newest.Should().BePositive();
            current.Take(newest).Should().Equal(previous.Take(newest), $"the fold at call {call} keeps the prefix");
        }
        runner.Parts.Select(p => p.InputBlocks.Sum(b => b.Length)).Max()
            .Should().BeLessThan(Config.MaxContextTokens * 4, "the input stays inside the window");
    }

    [Fact]
    public async Task Loop_ATargetAtTheTrigger_FoldsOnEveryCallPastIt_AsTheOldTailDid()
    {
        // The regime the replay showed, reproduced by a target that leaves no room: once past the trigger, each call
        // folds. It pins that the target, not the round count, is what spaces the folds.
        var runner = new ScriptedWorkerProcessRunner();
        for (var i = 0; i < 14; i++) runner.EnqueueToolCall("read_file", $$"""{"path":"src/F{{i}}.cs"}""");
        runner.EnqueueText("done");
        var folds = 0;
        var config = new CompactionConfig
        {
            IsEnabled = true, MaxContextTokens = 10_000, MaxContextTokensTriggerRatio = 0.3, FoldTargetRatio = 0.3,
            KeepRecentIterations = 3,
        };

        await RunLoop(runner, new CompactingChatClient(Bridge(runner), config, null,
            (_, _) => Task.FromResult($"SUMMARY {++folds}")));

        folds.Should().BeGreaterThan(8, "a fold that keeps three 1,000-token rounds stays at a 3,000 trigger");
    }

    private static ExternalWorkerChatClient Bridge(ScriptedWorkerProcessRunner runner)
    {
        var json = new WorkerJsonFormat();
        return new ExternalWorkerChatClient(
            new WorkerRequestComposer(new WorkerMessageMapper(json), new WorkerOptionsMapper()),
            new WorkerPromptRenderer(json), new WorkerReplyParser(json), new WorkerReplyTranslator(), runner, Context,
            new ExternalWorkerCliOptions("claude", ["-p"], TimeSpan.FromMinutes(5), "/tmp"),
            "external_worker", "sonnet", NullLogger.Instance);
    }

    private static readonly AsyncLocalRunContextAccessor Context = new();

    private static async Task RunLoop(ScriptedWorkerProcessRunner runner, IChatClient compacting)
    {
        var tool = AIFunctionFactory.Create((string path) => $"FILE {path}\n" + new string('x', 4_000), "read_file", "Reads a file");
        using var run = Context.BeginScope("run-42");
        using var step = Context.BeginStepScope(7);
        using var call = Context.BeginCallScope("agentic-executor", "Implementation", "primary");
        await new ChatClientBuilder(compacting).UseFunctionInvocation(configure: f => f.MaximumIterationsPerRequest = 40).Build()
            .GetResponseAsync([new(ChatRole.System, "You are the reader."), new(ChatRole.User, "Read every file.")],
                new ChatOptions { Tools = [tool] }, CancellationToken.None);
        runner.Parts.Should().NotBeEmpty();
    }
}
