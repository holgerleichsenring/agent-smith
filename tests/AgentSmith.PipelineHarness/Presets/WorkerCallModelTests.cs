using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-7c9bf through the production chat-client factory: a worker call whose CLI ran opus and asked
/// haiku on the side is recorded as an opus call at the CLI's total cost, with haiku as secondary usage —
/// every recorded call used to read "claude-haiku-4-5-20251001+claude-opus-5-5".
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class WorkerCallModelTests
{
    [Fact]
    public async Task RealComposition_WorkerCallWithSideModel_RecordsThePrimaryModelAndTheCliCost()
    {
        var answer = JsonSerializer.Serialize(new WorkerJsonFormat().Serialize(new WorkerReply(Text: "The guard exists.")));
        var stream = string.Join("\n",
            """{"type":"system","subtype":"init","session_id":"s","model":"claude-opus-5-5"}""",
            """{"type":"result","subtype":"success","is_error":false,"num_turns":1,"result":""" + answer + ""","total_cost_usd":0.4213,"usage":{"input_tokens":2,"output_tokens":900,"cache_read_input_tokens":12149,"cache_creation_input_tokens":57017},"modelUsage":{"claude-haiku-4-5-20251001":{"inputTokens":412,"outputTokens":20,"costUSD":0.0005},"claude-opus-5-5":{"inputTokens":2,"outputTokens":900,"costUSD":0.4208}}}""");
        var worker = new ScriptedWorkerProcessRunner().EnqueueRaw(stream);
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), ExternalWorkerHarness.DrivenBy(worker));
        var runContext = harness.Services.GetRequiredService<IRunContextAccessor>();
        var ledger = new RecordedCallLedger();
        using var run = runContext.BeginScope("run-7c9bf");
        using var calls = runContext.BeginRecordedCallsScope(ledger);

        var client = harness.Services.GetRequiredService<IChatClientFactory>().Create(ExternalWorkerHarness.Agent(), TaskType.Summarization);
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Is the guard there?")]);

        response.Text.Should().Be("The guard exists.");
        var call = ledger.Calls.Should().ContainSingle().Subject;
        call.Model.Should().Be("claude-opus-5-5");
        call.CostUsd.Should().Be(0.4213m, "the CLI's total, haiku included — pricing is unchanged");
        call.Secondary.Should().ContainSingle().Which.Model.Should().Be("claude-haiku-4-5-20251001");
    }
}
