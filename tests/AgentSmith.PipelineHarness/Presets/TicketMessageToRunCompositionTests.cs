using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Turns;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-4860b fast-tier end-to-end: a code run working a spec of a ticket reads what the ticket's conversation
/// addressed to it — the message reaches the master appended to its next tool result, once. The conversation store is
/// the boundary here (the server's port over a real store is TicketMessageToRunTests).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class TicketMessageToRunCompositionTests
{

    private sealed class OneMessage : ITicketConversationMessages
    {
        public Task<DateTimeOffset?> LastActivityAsync(string project, string ticketId, CancellationToken ct) =>
            Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task<IReadOnlyList<RunAddressedMessage>> AddressedToAsync(
            string project, string ticketId, string runId, int afterIndex, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RunAddressedMessage>>(afterIndex < 4
                ? [new RunAddressedMessage(4, "also answer a whitespace-only body with 400", DateTimeOffset.UtcNow)]
                : []);
    }

    [Fact]
    public async Task Harness_AConversationMessageToTheRun_ReachesTheMasterWithItsNextToolResult()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(new RecordingTicketProvider()));
            services.AddSingleton<ITicketConversationMessages>(new OneMessage());
        });
        harness.ChatClient
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueFinish();
        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" }
            .WithSeries(new HarnessSeries.Member("Introduce the empty-payload guard", "An empty request body is answered with 400."));
        runner.RequestContext[ContextKeys.ProjectName] = "fixture";

        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue();
        var handed = harness.ChatClient.CallMessages.SelectMany(call => call)
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? string.Empty)
            .Where(r => r.Contains(MidTurnMessageAIFunction.Heading)).ToList();
        handed.Should().NotBeEmpty("the master's tool result carries the operator's message");
        handed.Should().OnlyContain(r => r.Contains("also answer a whitespace-only body with 400"));
        handed.Distinct().Should().ContainSingle("it is handed over once");
        runner.LastContext!.Get<int>(ContextKeys.TicketMessagesThrough).Should().Be(4);
    }
}
