using System.Collections.Concurrent;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Passes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Passes;

/// <summary>2026-10-13-9a59f: the pieces the pass tests share — a capturing publisher, a spec step's
/// pipeline, verdict texts, and an open loop driven by a scripted runner.</summary>
internal static class MasterPassFixtures
{
    internal const string RunId = "2026-10-13T0900-pass";
    internal const string Spec = "2026-10-13-1234a";

    internal static string Verdict(string status, string? acceptance = null, string? extra = null) =>
        "Done.\n```verdict\n{ \"status\": \"" + status + "\", \"build_ran\": true, \"build_passed\": true, "
        + "\"tests_ran\": true, \"tests_passed\": true, \"summary\": \"s\""
        + (acceptance is null ? "" : ", \"acceptance\": " + acceptance)
        + (extra is null ? "" : ", " + extra) + " }\n```";

    internal static PipelineContext SpecPipeline(params string[] done)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.CodeName);
        if (done.Length > 0)
            pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft(Spec, "goal", $"spec: {Spec}", []) { Done = done });
        return pipeline;
    }

    internal static MasterPassRecorder Recorder(IEventPublisher events, IRunContextAccessor runContext) =>
        MasterHandlerFixture.Recorder(events, runContext);

    internal static ProgressLedger Ledger(ProgressStatus status) =>
        new([new ProgressLedgerEntry("s1", "do it", status)]);
}

/// <summary>2026-10-13-9a59f: keeps every published event.</summary>
internal sealed class CapturingEvents : IEventPublisher
{
    private readonly ConcurrentQueue<RunEvent> _events = new();

    public IReadOnlyList<RunEvent> All => [.. _events];

    public IReadOnlyList<MasterPassEndedEvent> Ends => [.. _events.OfType<MasterPassEndedEvent>()];

    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(runEvent);
        return Task.CompletedTask;
    }
}

/// <summary>2026-10-13-9a59f: one drive per script entry; the last entry repeats.</summary>
internal sealed class ScriptedPasses(params Func<AgenticLoopResult>[] script) : IAgenticLoopRunner
{
    private int _call;

    public int Calls => _call;

    public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(script[Math.Min(_call++, script.Length - 1)]());

    internal static Func<AgenticLoopResult> Text(string text, int toolCalls = 1) => () =>
    {
        var messages = Enumerable.Range(0, toolCalls)
            .Select(i => new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"c{i}", "read_file")]))
            .Append(new ChatMessage(ChatRole.Assistant, text)).ToList();
        return new AgenticLoopResult(new ChatResponse(messages), TimeSpan.FromSeconds(1));
    };

    internal static Func<AgenticLoopResult> Throws(Exception ex) => () => throw ex;
}

/// <summary>2026-10-13-9a59f: the open loop over a scripted runner, as the handler hands it over.</summary>
internal sealed class LoopRig
{
    public LoopRig(PipelineContext pipeline, ProgressStatus seed, IRunContextAccessor runContext, params Func<AgenticLoopResult>[] script)
    {
        Pipeline = pipeline;
        Runner = new ScriptedPasses(script);
        Progress = new ProgressLedgerToolHost([new ProgressLedgerEntry("s1", "do it", seed)]);
        Recorder = MasterPassFixtures.Recorder(Events, runContext);
        Clarifications = new TicketClarificationToolHost();
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["default"] = new Mock<ISandbox>().Object };
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        var context = MasterHandlerFixture.BuildContext("coding-agent-master") with { Pipeline = pipeline };
        var verdictOwed = new VerdictOwed("coding-agent-master", Progress.GetLedger, () => 0m, 1000, NullLogger.Instance);
        Session = new ReengageSession(context,
            new AgenticLoopRequest(new AgentConfig(), TaskType.Primary, "system", "user", []),
            "user", PipelinePresets.CodeName, Progress, new FilesystemToolHost(sandboxes, "default", "/work"),
            new LogDecisionToolHost(new MasterHandlerFixture.NoOpDecisionLogger(), null), verdictOwed,
            PipelineCostTracker.GetOrCreate(pipeline), _ => { }, Clarifications, new MasterConversation(), _ => { });
    }

    public PipelineContext Pipeline { get; }
    public ScriptedPasses Runner { get; }
    public ProgressLedgerToolHost Progress { get; }
    public CapturingEvents Events { get; } = new();
    public MasterPassRecorder Recorder { get; }
    public TicketClarificationToolHost Clarifications { get; }
    public ReengageSession Session { get; }

    /// <summary>Opens pass 0 as the handler does, then hands it to the loop with the verdict it emitted.</summary>
    public async Task RunAsync(AgentSmith.Domain.Models.MasterVerification? passZeroVerdict)
    {
        var pass0 = Recorder.Open(Pipeline, passInStep: 0);
        pass0?.Dispose();
        var loop = MasterHandlerFixture.Reengagement(Runner, Recorder);
        await loop.RunAsync(Session,
            new ReengageState(new AgenticLoopResult(new ChatResponse(), TimeSpan.Zero), [], passZeroVerdict),
            pass0, passZeroVerdict, CancellationToken.None);
    }
}
