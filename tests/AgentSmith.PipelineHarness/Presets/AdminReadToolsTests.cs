using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-f3a8b: an admin asks why a run stopped and where its cost went — the scripted master reads the run and
/// its calls through the real tools over the real store, and what it saw carries the end, the per-role cost and the
/// prompt id, with a configured secret value masked wherever a record held it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AdminReadToolsTests
{
    private const string RunId = "2026-11-17T10-00-00-ab12";
    private const string Secret = "tok-0123456789-secret";

    [Fact]
    public async Task AdminTurn_WhyDidTheRunStop_AnswersFromTheRecordsWithTheSecretMasked()
    {
        await using var harness = AdminTurnRunnerTests.BuildHarness(s =>
        {
            s.RemoveAll<ISecretValues>();
            s.AddSingleton<ISecretValues>(new OneSecret(Secret));
        });
        await SeedAsync(harness);
        var state = await AdminTurnRunnerTests.OpenAsync(harness, "why did the last run stop, and where did its cost go?");
        harness.ChatClient
            .EnqueueToolCall("read_run", $$"""{"runId": "{{RunId}}"}""")
            .EnqueueToolCall("read_llm_calls", $$"""{"runId": "{{RunId}}"}""")
            .EnqueueText($"Run {RunId} stopped on its cost cap at step 2; the master role spent $1.50 of $1.60.");

        var result = await AdminTurnRunnerTests.TurnAsync(harness, state);

        result!.Reply.Should().Contain(RunId);
        var seen = string.Join("\n", harness.ChatClient.CallMessages.SelectMany(m => m)
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString()));
        seen.Should().Contain("cost-cap-exhausted").And.Contain("\"key\":\"master\"").And.Contain("prompt-7");
        seen.Should().NotContain(Secret).And.Contain("[masked-secret]");
    }

    private static async Task SeedAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();
        db.Runs.Add(new Run
        {
            Id = RunId, Project = "fixture-fix-bug", Pipeline = "code", Status = "failed", EndKind = "cost-cap-exhausted",
            EndDetailJson = $$"""{"reason":"cap reached","echo":"{{Secret}}"}""", StartedAt = DateTimeOffset.UtcNow,
            CostTotalUsd = 1.60m,
        });
        db.RunSteps.Add(new RunStep { RunId = RunId, StepIndex = 2, StepName = "AgenticMaster", Status = "failed" });
        db.RunLlmCalls.Add(new RunLlmCall { RunId = RunId, Role = "master", Model = "m", TokensIn = 900, CostUsd = 1.50m, EventSeq = 5, CachedTokensIn = 400 });
        db.RunLlmCalls.Add(new RunLlmCall { RunId = RunId, Role = "reviewer", Model = "m", TokensIn = 90, CostUsd = 0.10m });
        db.RunEvents.Add(new RunEvent
        {
            RunId = RunId, Seq = 5, Type = "LlmCallFinished", Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = """{"PromptId":"prompt-7"}""",
        });
        await db.SaveChangesAsync();
    }

    private sealed class OneSecret(string value) : ISecretValues
    {
        public string? Resolve(string name) => value;

        public IReadOnlyList<string> All() => [value];
    }
}
