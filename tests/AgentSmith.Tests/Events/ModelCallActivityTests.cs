using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-09-17-042ee: a design turn's owner is told each model call, and told it WHEN IT
/// RETURNS — the intent sentence exists only then. Reporting at call start would show the
/// sentence the previous call narrated, which is worse than showing none.
/// </summary>
public sealed class ModelCallActivityTests
{
    [Fact]
    public async Task DialogTurn_ModelCall_IsReportedOnReturnWithItsIntent()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        var client = NewClient("Reading Router.cs to confirm the dispatch path.", accessor);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], options: null, CancellationToken.None);

        recorder.Lines.Should().ContainSingle().Which.Should()
            .Be("model sample-model Reading Router.cs to confirm the dispatch path.");
    }

    [Fact]
    public async Task ModelCall_WithNoObserverSet_ReportsNothing()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        accessor.Observe(recorder).Dispose(); // the turn that had set one has ended
        var client = NewClient("Reading Router.cs.", accessor);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], options: null, CancellationToken.None);

        recorder.Seen.Should().BeEmpty("a coding run sets no observer, and a coding run reports nothing");
    }

    [Fact]
    public async Task ModelCall_WhenTheReportThrows_PublishesOneFinishedEventNotTwo()
    {
        var accessor = TurnActivityRecorder.Silent();
        using var observing = accessor.Observe(new ThrowingObserver());
        var publisher = EventTestStubs.Recording();
        var client = NewClient("Reading Router.cs.", accessor, publisher);

        var act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], options: null, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        publisher.Events.OfType<LlmCallFinishedEvent>().Should().ContainSingle(
            "a failed progress line is not the CALL failing — a second event is a duplicate cost row")
            .Which.Outcome.Should().Be(WorkOutcome.Ok);
    }


    [Fact]
    public async Task ModelCall_WithNoAccessorInjected_StillAnswers()
    {
        var client = NewClient("Reading Router.cs.", activity: null);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], options: null, CancellationToken.None);

        response.Text.Should().Contain("Router.cs");
    }

    private static EventPublishingChatClient NewClient(
        string assistantText, AgentSmith.Contracts.Turns.ITurnActivityObserverAccessor? activity,
        AgentSmith.Tests.TestHelpers.RecordingEventPublisher? publisher = null) =>
        NewClient(new StubChat(assistantText), activity, publisher);

    private static EventPublishingChatClient NewClient(
        IChatClient chat, AgentSmith.Contracts.Turns.ITurnActivityObserverAccessor? activity,
        AgentSmith.Tests.TestHelpers.RecordingEventPublisher? publisher = null) =>
        new(chat, publisher ?? (IEventPublisher)new NoOpEventPublisher(), new FixedRunContext("run-042ee"),
            new LlmCallCostCalculator(
                new ModelPricingResolver(new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase))),
            new ThrottleWaitReporter(), "sample-model", activity);

    /// <summary>Reports by throwing, as the observer contract forbids and a test double may.</summary>
    private sealed class ThrowingObserver : AgentSmith.Contracts.Turns.ITurnActivityObserver
    {
        public Task ReportAsync(
            AgentSmith.Contracts.Turns.TurnActivity activity, CancellationToken cancellationToken) =>
            throw new OperationCanceledException("the push was cancelled");
    }

    /// <summary>A run id with no call scope: LlmCall events are published, nothing reads the intent.</summary>
    private sealed class FixedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string id) => new NoOp();
        public int? CurrentStepIndex => null;
        public string? CurrentPhaseId => null;
        public IDisposable BeginStepScope(int stepIndex, string? phaseId = null) => new NoOp();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOp();
        private sealed class NoOp : IDisposable { public void Dispose() { } }
    }

    private sealed class StubChat(string assistantText) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, assistantText)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
