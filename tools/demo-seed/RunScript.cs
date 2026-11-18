using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Services;

namespace DemoSeed;

/// <summary>
/// Writes one run the way a real one is written: as the event stream a pipeline publishes, fed
/// through the server's own projector, so every row the run page reads is the row a run leaves.
/// </summary>
internal sealed class RunScript(RunDbProjector projector, string runId, DateTimeOffset start)
{
    private readonly List<RunEvent> _events = [];
    private DateTimeOffset _now = start;
    private int _step;
    private int _total;
    private (int Index, string? Spec)? _current;

    public string RunId => runId;
    public DateTimeOffset Now => _now;

    public RunScript Start(string project, string pipeline, string ticketId, IReadOnlyList<string> repos,
        IReadOnlyList<string> plannedSteps, string trigger = "dashboard")
    {
        _total = plannedSteps.Count;
        _events.Add(new RunStartedEvent(runId, trigger, pipeline, repos, _now, "claude-default", ticketId,
            plannedSteps, project, "github"));
        return this;
    }

    public RunScript Ticket(string ticketId, string title, string body, string? acceptance = null, string? url = null)
    {
        _events.Add(new TicketFetchedEvent(runId, ticketId, title, body, "open", ["shop"], 0, "github", Tick(2),
            WebUrl: url, AcceptanceCriteria: acceptance));
        return this;
    }

    /// <summary>One step: started, its body (llm calls, tools, decisions), finished.</summary>
    public RunScript Step(string command, string? display = null, string? specId = null, int seconds = 4,
        Action<StepBody>? body = null, string status = "success", string? reason = null)
    {
        var index = _step++;
        var startedAt = Tick(1);
        _events.Add(new StepStartedEvent(runId, index, command, _total, startedAt, display, command, specId));
        _current = (index, specId);
        body?.Invoke(new StepBody(this, index, specId));
        _current = null;
        if (status == "running") return this; // the step is still going: no finish event yet
        var finishedAt = Tick(seconds);
        _events.Add(new StepFinishedEvent(runId, index, status, (long)(finishedAt - startedAt).TotalMilliseconds,
            finishedAt, reason));
        return this;
    }

    public RunScript Add(RunEvent e) { Push(e); return this; }

    // Events inside a step carry its index and spec, as the step-attributing publisher stamps them.
    private void Push(RunEvent e) =>
        _events.Add(_current is { } c ? e with { OriginStepIndex = c.Index, OriginSpecId = c.Spec } : e);

    public DateTimeOffset Tick(double seconds) => _now = _now.AddSeconds(seconds);

    public RunScript Finish(string status, string summary, decimal cost, string? prUrl = null, string? endKind = null)
    {
        _events.Add(new RunFinishedEvent(runId, status, prUrl, summary, Tick(2), cost, endKind));
        return this;
    }

    /// <summary>Feeds the stream through the projector and drains the trail, as the server would.</summary>
    public async Task WriteAsync(CancellationToken ct)
    {
        foreach (var e in _events) await projector.ProjectAsync(e, ct);
        await projector.FlushAllAsync(ct);
    }

    internal sealed class StepBody(RunScript run, int index, string? specId)
    {
        public StepBody Llm(string role, string model, long tokensIn, long tokensOut, decimal cost, double seconds,
            long cached = 0, string? stage = null, string? repo = null)
        {
            var at = run.Tick(0.2);
            run.Push(new LlmCallStartedEvent(run.RunId, model, role, Guid.NewGuid().ToString("N")[..16], at,
                stage, repo, tokensIn * 4));
            var done = run.Tick(seconds);
            run.Push(new LlmCallFinishedEvent(run.RunId, model, role, tokensIn, tokensOut, cost,
                (long)(seconds * 1000), done, stage, repo, cached, 0, 0, tokensIn * 4, tokensOut * 4));
            return this;
        }

        public StepBody Tool(string tool, string summary, bool ok = true, string? repo = null, string? role = "primary",
            string? error = null, double seconds = 0.6)
        {
            run.Push(new ToolCallEvent(run.RunId, tool, summary.Length, run.Tick(0.1), summary, role, null, repo));
            run.Push(new ToolResultEvent(run.RunId, tool, ok, 400, run.Tick(seconds), error, role, null, repo,
                DurationMs: (long)(seconds * 1000)));
            return this;
        }

        public StepBody Decision(string category, string chose, string? over, string reason)
        {
            run.Push(new DecisionLoggedEvent(run.RunId, category, chose, over, reason, run.Tick(0.3), specId));
            return this;
        }

        public StepBody Detail(string origin, string detail)
        {
            run.Push(new L1StepDetailEvent(run.RunId, index, origin, detail, run.Tick(0.2)));
            return this;
        }

        public StepBody Raw(RunEvent e)
        {
            run.Push(e);
            return this;
        }
    }
}
