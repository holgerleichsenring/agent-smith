using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SubAgents;

public sealed class SubAgentRunnerTests
{
    private static SubAgentSpec Spec(string name) =>
        new(name, $"do {name}", $"task body {name}",
            new InheritedContext("goal", "slice"));

    [Fact]
    public async Task SubAgentRunner_DeterministicSpecOrderMerge()
    {
        var stub = new StubLoopRunner(reverseOrderDelays: true);
        var sut = BuildRunner(stub);
        var specs = new[] { Spec("FirstScout"), Spec("SecondAuditor"), Spec("ThirdInspector") };

        var results = await sut.RunAsync(specs, BuildContext(), CancellationToken.None);

        results.Should().HaveCount(3);
        results[0].Name.Should().Be("FirstScout");
        results[1].Name.Should().Be("SecondAuditor");
        results[2].Name.Should().Be("ThirdInspector");
        results.Select(r => r.TaskIndex).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task SubAgentRunner_SemaphoreCapsInFlightAtMaxConcurrent()
    {
        var stub = new StubLoopRunner(holdGate: true);
        var sut = BuildRunner(stub, maxConcurrent: 2);
        var specs = Enumerable.Range(0, 6).Select(i => Spec($"Slot{i}Investigator")).ToArray();

        var runTask = sut.RunAsync(specs, BuildContext(), CancellationToken.None);
        await Task.Delay(80);
        stub.PeakInFlight.Should().BeLessThanOrEqualTo(2);
        stub.Release();
        await runTask;

        stub.PeakInFlight.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task SubAgentRunner_OneChildFails_SiblingsContinue()
    {
        var stub = new StubLoopRunner(failOnName: "PoisonAuditor");
        var sut = BuildRunner(stub);
        var specs = new[] { Spec("FirstScout"), Spec("PoisonAuditor"), Spec("ThirdInspector") };

        var results = await sut.RunAsync(specs, BuildContext(), CancellationToken.None);

        results[0].Status.Should().Be(SubAgentStatus.Succeeded);
        results[1].Status.Should().Be(SubAgentStatus.Failed);
        results[2].Status.Should().Be(SubAgentStatus.Succeeded);
    }

    [Fact]
    public async Task SubAgentRunner_EmitsSpawnedAndCompletedEventsPerChild()
    {
        var stub = new StubLoopRunner();
        var publisher = new RecordingEventPublisher();
        var sut = BuildRunner(stub, publisher: publisher);
        var specs = new[] { Spec("FirstScout"), Spec("SecondAuditor") };

        await sut.RunAsync(specs, BuildContext(), CancellationToken.None);

        publisher.Events.OfType<SubAgentSpawnedEvent>().Should().HaveCount(2);
        publisher.Events.OfType<SubAgentCompletedEvent>().Should().HaveCount(2);
    }

    [Fact]
    public async Task SubAgentRunner_ChildrenShareRunSandbox_NoNewSandboxSpawned()
    {
        var stub = new StubLoopRunner();
        var sut = BuildRunner(stub);
        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["default"] = new NullSandbox(),
        };
        var context = BuildContext(sandboxes);

        await sut.RunAsync(new[] { Spec("FirstScout") }, context, CancellationToken.None);

        sandboxes.Should().HaveCount(1, "no new sandbox spawned for the child");
    }

    [Fact]
    public async Task SubAgentRunner_ChildrenShareRunCostTracker_TokensCountToRunTotal()
    {
        var stub = new StubLoopRunner();
        var sut = BuildRunner(stub);
        var context = BuildContext();
        var startingCalls = context.CostTracker.CallCount;

        await sut.RunAsync(new[] { Spec("FirstScout"), Spec("SecondAuditor") },
            context, CancellationToken.None);

        context.CostTracker.CallCount.Should().Be(startingCalls + 2);
    }

    [Fact]
    public async Task SubAgentRunner_InheritedContextPropagatesToChildLoop()
    {
        var stub = new StubLoopRunner();
        var sut = BuildRunner(stub);
        var spec = new SubAgentSpec(
            "FirstScout", "scout the repo", "list source roots",
            new InheritedContext("global goal X", "context Y"));

        await sut.RunAsync(new[] { spec }, BuildContext(), CancellationToken.None);

        stub.SeenRequests.Should().HaveCount(1);
        stub.SeenRequests[0].InheritedContext.Should().NotBeNull();
        stub.SeenRequests[0].InheritedContext!.PipelineGoal.Should().Be("global goal X");
        stub.SeenRequests[0].InheritedContext!.PriorContextSlice.Should().Be("context Y");
        stub.SeenRequests[0].Name.Should().Be("FirstScout");
    }

    private static SubAgentRunner BuildRunner(
        IAgenticLoopRunner loopRunner,
        int maxConcurrent = 4,
        IEventPublisher? publisher = null)
    {
        var limits = new LoopLimitsConfig { MaxConcurrentSubAgents = maxConcurrent };
        var pub = publisher ?? new RecordingEventPublisher();
        return new SubAgentRunner(loopRunner, pub, limits, NullLogger<SubAgentRunner>.Instance);
    }

    private static SubAgentContext BuildContext(
        IReadOnlyDictionary<string, ISandbox>? sandboxes = null)
    {
        var pipeline = new PipelineContext();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        var toolKit = new ToolKit(new AllHostsActivePolicy());
        return new SubAgentContext(
            pipeline,
            sandboxes ?? new Dictionary<string, ISandbox>(),
            tracker,
            MasterRunId: "run-test",
            toolKit,
            new AllHostsActivePolicy(),
            new SubAgentBudget(maxPerRun: 100));
    }

    private sealed class NullSandbox : ISandbox
    {
        public string JobId => "test-job";
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<AgentSmith.Sandbox.Wire.StepResult> RunStepAsync(
            AgentSmith.Sandbox.Wire.Step step,
            IProgress<AgentSmith.Sandbox.Wire.StepEvent>? progress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

internal sealed class StubLoopRunner : IAgenticLoopRunner
{
    private readonly bool _reverseOrderDelays;
    private readonly bool _holdGate;
    private readonly string? _failOnName;
    private readonly TaskCompletionSource _gate = new();
    private int _inFlight;
    private int _peakInFlight;
    private readonly object _lock = new();
    private readonly List<AgenticLoopRequest> _seen = new();

    public StubLoopRunner(
        bool reverseOrderDelays = false,
        bool holdGate = false,
        string? failOnName = null)
    {
        _reverseOrderDelays = reverseOrderDelays;
        _holdGate = holdGate;
        _failOnName = failOnName;
    }

    public int PeakInFlight { get { lock (_lock) return _peakInFlight; } }
    public IReadOnlyList<AgenticLoopRequest> SeenRequests
    {
        get { lock (_lock) return _seen.ToArray(); }
    }

    public void Release() => _gate.TrySetResult();

    public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _seen.Add(request);
            _inFlight++;
            if (_inFlight > _peakInFlight) _peakInFlight = _inFlight;
        }

        try
        {
            if (_failOnName is not null && request.Name == _failOnName)
                throw new InvalidOperationException("poisoned");

            if (_reverseOrderDelays && request.Name is not null && request.Name.StartsWith("First"))
                await Task.Delay(40, cancellationToken);

            if (_holdGate) await _gate.Task;

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 },
            };
            return new AgenticLoopResult(response, TimeSpan.FromMilliseconds(10));
        }
        finally
        {
            lock (_lock) _inFlight--;
        }
    }
}
