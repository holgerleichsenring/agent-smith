using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Tests.Helpers;

/// <summary>
/// p0142 test helper: in-memory ISkillCallRuntime that returns canned
/// SkillCallResult values per ExecuteAsync invocation. Captures the last
/// SkillCallRequest so tests can assert ToolSet / Phase / InvestigatorMode
/// shape without standing up the full runtime collaborator chain.
/// </summary>
public sealed class StubSkillCallRuntime : ISkillCallRuntime
{
    private readonly Queue<SkillCallResult> _results = new();
    private readonly Func<string, string>? _outputFromSkillName;
    public List<SkillCallRequest> Requests { get; } = [];

    public StubSkillCallRuntime() { }
    public StubSkillCallRuntime(Func<string, string> outputFromSkillName)
        => _outputFromSkillName = outputFromSkillName;

    public StubSkillCallRuntime Returns(SkillCallResult result)
    {
        _results.Enqueue(result);
        return this;
    }

    public StubSkillCallRuntime ReturnsOk(string output, string? hitLimit = null)
        => Returns(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Ok,
            Output = output,
            Cost = MakeRecord(hitLimit),
            Trace = Array.Empty<LoopTraceEntry>()
        });

    public StubSkillCallRuntime ReturnsIncomplete(string output, string hitLimit = "tokens")
        => Returns(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Incomplete,
            Output = output,
            Cost = MakeRecord(hitLimit),
            Trace = Array.Empty<LoopTraceEntry>()
        });

    public StubSkillCallRuntime ReturnsFailedParse(string reason = "parse failed")
        => Returns(new SkillCallResult
        {
            Outcome = SkillCallOutcome.FailedParse,
            Cost = MakeRecord(null),
            Trace = Array.Empty<LoopTraceEntry>(),
            FailureReason = reason
        });

    public Task<SkillCallResult> ExecuteAsync(
        SkillCallRequest request, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_results.Count > 0)
            return Task.FromResult(_results.Dequeue());
        var output = _outputFromSkillName?.Invoke(request.SkillName) ?? "{}";
        return Task.FromResult(new SkillCallResult
        {
            Outcome = SkillCallOutcome.Ok,
            Output = output,
            Cost = new CallCostRecord
            {
                SkillName = request.SkillName,
                Role = request.Role,
                Phase = request.Phase,
                StartedAt = DateTimeOffset.UtcNow
            },
            Trace = Array.Empty<LoopTraceEntry>()
        });
    }

    private static CallCostRecord MakeRecord(string? hitLimit)
        => new()
        {
            SkillName = "stub",
            Role = "stub",
            Phase = SkillExecutionPhase.Plan,
            StartedAt = DateTimeOffset.UtcNow,
            HitLimit = hitLimit
        };
}
