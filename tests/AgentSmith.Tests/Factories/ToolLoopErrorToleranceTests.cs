using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Factories.ToolLoopErrorToleranceSupport;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-19-c511c: how many failing tool calls a master survives, and what the model reads of a
/// failure, are this product's numbers. Run 2026-09-17T14-41-51-a3e2 died on its fourth failing
/// log_decision because the framework's default allowed three and nobody had chosen it.
/// </summary>
public sealed class ToolLoopErrorToleranceTests
{
    private const string Denied = "path denied: /etc/shadow";

    [Fact]
    public void FunctionInvocation_ConsecutiveErrorLimit_IsTheValueThisProductDeclares()
    {
        foreach (var task in ChatClientFactory.ToolBearingTasks)
            Loop(LiveFactory(new ToolCallingProvider(0)), task).MaximumConsecutiveErrorsPerRequest
                .Should().Be(3, "three failing iterations in a row mean the loop is stuck ({0})", task);
    }

    [Fact]
    public void FunctionInvocation_DetailedErrors_AreTheValueThisProductDeclares()
    {
        foreach (var task in ChatClientFactory.ToolBearingTasks)
            Loop(LiveFactory(new ToolCallingProvider(0)), task).IncludeDetailedErrors
                .Should().BeTrue("a model that cannot see why a call failed can only reword it ({0})", task);
    }

    [Fact]
    public void ReplayFactory_CarriesTheSameErrorConfigurationAsTheLiveFactory()
    {
        foreach (var task in ChatClientFactory.ToolBearingTasks)
        {
            var live = Loop(LiveFactory(new ToolCallingProvider(0)), task);
            var replay = Loop(new ReplayChatClientFactory(new ToolCallingProvider(0)), task);

            (replay.MaximumConsecutiveErrorsPerRequest, replay.IncludeDetailedErrors).Should().Be(
                (live.MaximumConsecutiveErrorsPerRequest, live.IncludeDetailedErrors),
                "a replayed run has to fail at the call where the live one did ({0})", task);
        }
    }

    [Fact]
    public async Task ToolThrow_BelowTheLimit_ReachesTheModelAndTheLoopGoesOn()
    {
        var provider = new ToolCallingProvider(calls: 3);

        var result = await RunAsync(provider, Throwing(() => new InvalidOperationException(Denied)));

        result.Response.Text.Should().Be(Answer);
        provider.ResultsSeen.Select(r => r.Result?.ToString()).Should().HaveCount(3)
            .And.OnlyContain(text => text!.Contains(Denied), "the model reads why each call failed");
    }

    [Fact]
    public async Task ToolThrow_AtTheLimit_EndsTheMasterAsAClassifiedFailure()
    {
        var provider = new ToolCallingProvider(calls: 4);

        var act = () => RunAsync(provider, Throwing(() => new InvalidOperationException(Denied)));

        var thrown = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        provider.Requests.Should().HaveCount(4, "the fourth failure rethrows instead of asking again");
        MasterOutcomes.DescribeMasterFailure(thrown).Should()
            .Be($"The coding agent failed: InvalidOperationException: {Denied}");
    }

    [Fact]
    public async Task ToolThrow_Cancellation_StillStopsTheRun()
    {
        using var cancel = new CancellationTokenSource();
        var provider = new ToolCallingProvider(calls: 3);
        var tool = Tool(_ => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); });

        var act = () => RunAsync(provider, tool, cancellationToken: cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.Requests.Should().HaveCount(1, "a cancelled run is not a failure the model may retry");
    }

    [Fact]
    public async Task ToolThrow_MasterBudgetExhausted_StillStopsTheRunCleanly()
    {
        var failures = 0;
        var provider = new ToolCallingProvider(calls: 3);
        var hooks = new MasterLoopHooks(IsBudgetExhausted: () => failures > 0, RenderReminder: () => null);
        var tool = Tool(_ => { failures++; throw new InvalidOperationException(Denied); });

        var act = () => RunAsync(provider, tool, hooks);

        await act.Should().ThrowAsync<MasterBudgetExhaustedException>(
            "the money fence reaches the master handler's own catch, not the tool-error count");
        failures.Should().Be(1);
    }

    private static FunctionInvokingChatClient Loop(Contracts.Services.IChatClientFactory factory, TaskType task) =>
        factory.Create(Agent(), task).Should().BeAssignableTo<FunctionInvokingChatClient>().Subject;

    private static AITool Throwing(Func<Exception> failure) => Tool(_ => throw failure());

    private static Task<AgenticLoopResult> RunAsync(
        ToolCallingProvider provider, AITool tool, MasterLoopHooks? hooks = null,
        CancellationToken cancellationToken = default) =>
        new AgenticLoopRunner(LiveFactory(provider), EventTestStubs.RunContext, NullLogger<AgenticLoopRunner>.Instance)
            .RunAsync(new AgenticLoopRequest(Agent(), TaskType.Primary, "system", "do the work", [tool],
                MasterLoopHooks: hooks), cancellationToken);
}
