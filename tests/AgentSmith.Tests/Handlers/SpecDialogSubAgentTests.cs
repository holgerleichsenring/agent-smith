using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-22-5891: a design conversation fans out into read-only children under a bound
/// that binds — its own fan-out count, its own child iteration ceiling — and gains none of
/// the tools a coding master gets on the way there.
/// </summary>
public sealed class SpecDialogSubAgentTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";

    [Fact]
    public async Task SpecDialogTurn_MasterSurface_CarriesSpawnAgentsAndReadObservations()
    {
        var loop = new DesignLoopRunner();

        await RunDesignTurnAsync(loop);

        MasterTools(loop).Should().Contain("spawn_agents").And.Contain("read_sub_agent_observations");
    }

    [Fact]
    public async Task SpecDialogTurn_MasterSurface_HasNoEnsureRepoSandboxAndNoUpdateProgress()
    {
        var loop = new DesignLoopRunner();

        await RunDesignTurnAsync(loop);

        MasterTools(loop).Should()
            .NotContain("ensure_repo_sandbox", "a writable sandbox ends the read-only guarantee")
            .And.NotContain("update_progress", "a conversation keeps no checklist");
    }

    [Fact]
    public async Task CodingTurn_MasterSurface_StillCarriesBothOfThose()
    {
        var loop = new DesignLoopRunner();
        var prompts = new MasterHandlerFixture.StubPromptCatalog(CodingMaster, "body");

        await MasterHandlerFixture.Build(loop, prompts, maxSubAgents: 20)
            .ExecuteAsync(MasterHandlerFixture.BuildContext(CodingMaster), CancellationToken.None);

        MasterTools(loop).Should().Contain("ensure_repo_sandbox").And.Contain("update_progress");
    }

    [Fact]
    public async Task SpecDialogTurn_ChildTools_CarryNoWriteNoRunAndNoAskHuman()
    {
        var loop = new DesignLoopRunner(Tasks("RepoScout"));
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await RunDesignTurnAsync(loop, children);

        var childTools = ChildTools(children);
        childTools.Should().Contain("read_file", "a child is there to read");
        childTools.Should().NotContain("write_file").And.NotContain("run_command");
        childTools.Should().NotContain(
            "ask_human", "one pending question per session — two children would strand each other");
    }

    [Fact]
    public async Task SpecDialogTurn_ChildTools_CarryNoMemoryWrite()
    {
        var loop = new DesignLoopRunner(Tasks("RepoScout"));
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await RunDesignTurnAsync(loop, children);

        var childTools = ChildTools(children);
        childTools.Should().Contain("recall", "reading memory is a read");
        childTools.Should().NotContain(
            "remember", "the read-only scope refuses that write by throwing, and a child dies on errors");
    }

    [Fact]
    public async Task SpecDialogTurn_ChildLoop_IsBoundedByTheDialogCeiling()
    {
        // The real runner over a capturing child loop: what the child's loop request carries
        // as its ceiling is the only bound on a child that gets no governor hooks.
        var childLoop = new CapturingChildLoop();
        var loop = new DesignLoopRunner(Tasks("RepoScout"));
        var context = DesignContext();
        context.AgentConfig.MaxSubAgentLoopIterations = 100;

        await RunAsync(loop, context, subAgents: RealRunner(childLoop),
            limits: new LoopLimitsConfig { MaxDialogSubAgentLoopIterations = 7 });

        childLoop.SeenRequests.Should().ContainSingle();
        childLoop.SeenRequests[0].MaxIterations.Should().Be(
            7, "a design turn's child runs under the dialog ceiling, not the 100-iteration one");
    }

    [Fact]
    public async Task SpecDialogTurn_AFanOutBeyondTheDialogCount_IsDeferredNotRun()
    {
        var loop = new DesignLoopRunner(Tasks("RepoScout", "ServiceAuditor", "ContractInspector"));
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await RunDesignTurnAsync(
            loop, children, new LoopLimitsConfig { MaxSubAgentsPerDialogTurn = 2 });

        children.SeenWaves.Should().ContainSingle().Which.Should().HaveCount(2);
        loop.SpawnResult.Should().Contain("budget_exhausted");
    }

    [Fact]
    public async Task SpecDialogTurn_AFanOutBeyondTheDialogCount_IsDeferredEvenWhenTheRunWideLimitIsHigher()
    {
        // The injected budget's capacity is frozen from the run-wide 20 the fixture wires;
        // a design turn that only READ its own count would still be granted all three.
        var loop = new DesignLoopRunner(Tasks("RepoScout", "ServiceAuditor", "ContractInspector"));
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await RunDesignTurnAsync(
            loop, children,
            new LoopLimitsConfig { MaxSubAgentsPerRun = 20, MaxSubAgentsPerDialogTurn = 1 });

        children.SeenWaves.Should().ContainSingle().Which.Should().ContainSingle();
        loop.SpawnResult.Should().Contain("budget_exhausted");
    }

    [Fact]
    public async Task SpecDialogTurn_ARunWideLimitOfZero_StillHonoursTheDialogLimit()
    {
        var loop = new DesignLoopRunner(Tasks("RepoScout", "ServiceAuditor"));
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await RunDesignTurnAsync(
            loop, children,
            new LoopLimitsConfig { MaxSubAgentsPerRun = 0, MaxSubAgentsPerDialogTurn = 2 });

        MasterTools(loop).Should().Contain("spawn_agents", "the design turn gates on its own count");
        children.SeenWaves.Should().ContainSingle().Which.Should().HaveCount(2);
    }

    [Fact]
    public async Task SpecDialogTurn_ADialogCountOfZero_OffersNoSpawnTool()
    {
        var loop = new DesignLoopRunner();

        await RunDesignTurnAsync(
            loop, limits: new LoopLimitsConfig { MaxSubAgentsPerRun = 20, MaxSubAgentsPerDialogTurn = 0 });

        MasterTools(loop).Should()
            .NotContain("spawn_agents").And.NotContain("read_sub_agent_observations");
    }

    [Fact]
    public async Task SpecDialogTurn_AZeroCountDesignTurn_IsStillReported()
    {
        // The turn that spawns nothing leaves the surface through the fan-out gate. Reported
        // there too, or the operator's page loses every tool line for the whole turn.
        var loop = new DesignLoopRunner();

        await RunDesignTurnAsync(
            loop, limits: new LoopLimitsConfig { MaxSubAgentsPerDialogTurn = 0 });

        loop.SeenRequests[0].Tools.OfType<AIFunction>().Should().NotBeEmpty()
            .And.OnlyContain(tool => tool is ActivityReportingAIFunction);
    }

    [Fact]
    public async Task SpecDialogTurn_TheSpawnCall_IsReportedAsTurnActivity()
    {
        var recorder = new TurnActivityRecorder();
        using var observing = TurnActivityRecorder.Silent().Observe(recorder);
        var loop = new DesignLoopRunner(Tasks("RepoScout"));

        await RunDesignTurnAsync(loop);

        recorder.Seen.Should().Contain(
            a => a.Kind == TurnActivityKind.Tool && a.Name == "spawn_agents",
            "the fan-out is otherwise the one call an operator cannot see");
    }

    [Fact]
    public async Task CodingTurn_MasterFunctions_AreNotWrappedForTurnReporting()
    {
        var loop = new DesignLoopRunner();
        var prompts = new MasterHandlerFixture.StubPromptCatalog(CodingMaster, "body");

        await MasterHandlerFixture.Build(loop, prompts, maxSubAgents: 20)
            .ExecuteAsync(MasterHandlerFixture.BuildContext(CodingMaster), CancellationToken.None);

        loop.SeenRequests[0].Tools.OfType<AIFunction>().Should().NotBeEmpty()
            .And.NotContain(tool => tool is ActivityReportingAIFunction);
    }

    [Fact]
    public async Task SpecDialogTurn_TwoChildrenReadingConcurrently_BothComplete()
    {
        var childLoop = new CapturingChildLoop(holdGate: true);
        var loop = new DesignLoopRunner(Tasks("RepoScout", "ServiceAuditor"));

        var turn = RunAsync(loop, DesignContext(), subAgents: RealRunner(childLoop));
        // Both children hold at the gate, so the peak IS how many the fan-out had running at
        // once: a serialised wave never leaves the first child and never reaches two.
        await TestWaits.UntilAsync(
            () => childLoop.PeakInFlight >= 2, "the two children read the same scope at once");
        childLoop.Release();
        await turn.OrHang("the design turn completes once its children are let go");

        loop.SpawnResult.Should().NotContain("Failed");
        loop.SpawnResult.Should().Contain("RepoScout").And.Contain("ServiceAuditor");
    }

    private static SubAgentRunner RealRunner(IAgenticLoopRunner childLoop) =>
        new(childLoop, new AgentSmith.Tests.Events.RecordingEventPublisher(),
            new LoopLimitsConfig { MaxConcurrentSubAgents = 4 },
            NullLogger<SubAgentRunner>.Instance);

    private static IReadOnlyList<string> MasterTools(DesignLoopRunner loop) =>
        [.. loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name)];

    private static IReadOnlyList<string> ChildTools(MasterHandlerFixture.StubSubAgentRunner children)
    {
        children.SeenContexts.Should().ContainSingle("the master spawned exactly one wave");
        return [.. children.SeenContexts[0].ChildTools.Select(t => t.Name)];
    }

    /// <summary>The spawn_agents argument for one child per name, each name non-generic.</summary>
    private static string Tasks(params string[] names) =>
        "[" + string.Join(",", names.Select(name =>
            "{\"name\":\"" + name + "\",\"activity\":\"read the sources\","
            + "\"task_description\":\"answer one question\",\"inherited_context\":"
            + "{\"pipeline_goal\":\"cut the phase\",\"prior_context_slice\":\"the transcript\"}}")) + "]";

    private static Task RunDesignTurnAsync(
        DesignLoopRunner loop, ISubAgentRunner? subAgents = null, LoopLimitsConfig? limits = null) =>
        RunAsync(loop, DesignContext(), subAgents, limits);

    private static Task RunAsync(
        DesignLoopRunner loop, AgenticMasterContext context,
        ISubAgentRunner? subAgents = null, LoopLimitsConfig? limits = null)
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body");
        return MasterHandlerFixture
            .Build(loop, prompts,
                subAgents: subAgents ?? new MasterHandlerFixture.StubSubAgentRunner(),
                limits: limits ?? new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);
    }

    private static AgenticMasterContext DesignContext()
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        // Discussed: an answered turn followed by an operator reply, so the early-proposal
        // refusal does not fire and the turn is an ordinary design turn.
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript,
            [
                new SpecDialogTurn(SpecDialogTurn.UserRole, "we need widgets"),
                new SpecDialogTurn(
                    SpecDialogTurn.AssistantRole, "Found the service; two open questions.",
                    SpecDialogTurnKind.Answer),
                new SpecDialogTurn(SpecDialogTurn.UserRole, "cut the ordering feature into slices"),
            ]);
        return context;
    }

    /// <summary>
    /// The master's loop: records what it was handed and, when given tasks, calls spawn_agents
    /// once — the only way a test reaches what the fan-out was granted.
    /// </summary>
    private sealed class DesignLoopRunner(string tasksJson = "") : IAgenticLoopRunner
    {
        public List<AgenticLoopRequest> SeenRequests { get; } = [];

        public string? SpawnResult { get; private set; }

        public async Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            SeenRequests.Add(request);
            if (SeenRequests.Count == 1 && tasksJson.Length > 0)
            {
                var spawn = request.Tools!.OfType<AIFunction>().Single(t => t.Name == "spawn_agents");
                var tasks = JsonDocument.Parse(tasksJson);
                var result = await spawn.InvokeAsync(
                    new AIFunctionArguments { ["tasks"] = tasks.RootElement }, cancellationToken);
                SpawnResult = result?.ToString() ?? string.Empty;
            }
            return new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// A child's loop: keeps every child request and, when gated, holds each child in flight
    /// until the test lets go, so <see cref="PeakInFlight"/> records how many the fan-out ever
    /// ran at once. The overlap is a COUNT the test polls, never a deadline the stub outlives.
    /// </summary>
    private sealed class CapturingChildLoop(bool holdGate = false) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        private readonly TaskCompletionSource _gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _lock = new();
        private int _inFlight;
        private int _peakInFlight;

        public IReadOnlyList<AgenticLoopRequest> SeenRequests
        {
            get { lock (_lock) return [.. _seen]; }
        }

        /// <summary>The most children the fan-out ever held in flight at the same moment.</summary>
        public int PeakInFlight { get { lock (_lock) return _peakInFlight; } }

        /// <summary>Let every held child finish.</summary>
        public void Release() => _gate.TrySetResult();

        public async Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _seen.Add(request);
                _inFlight++;
                if (_inFlight > _peakInFlight) _peakInFlight = _inFlight;
            }
            try
            {
                if (holdGate) await _gate.Task.WaitAsync(cancellationToken);
                return new AgenticLoopResult(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, "what the child read"))
                    {
                        Usage = new UsageDetails { InputTokenCount = 4, OutputTokenCount = 2 },
                    },
                    TimeSpan.FromMilliseconds(1));
            }
            finally
            {
                lock (_lock) _inFlight--;
            }
        }
    }
}
