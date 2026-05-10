using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Per-skill-call cost-attribution scope opened by
/// <see cref="PipelineCostTracker.BeginCall"/>. Inside the scope, every
/// <c>Track(ChatResponse)</c> call attributes its tokens to this scope's running
/// totals. Disposing the scope finalizes the <see cref="CallCostRecord"/>;
/// the runtime calls <see cref="Finalize"/> with its <see cref="LimitEnforcer"/>
/// counters before disposal.
/// </summary>
public sealed class SkillCallScope : IDisposable
{
    private readonly PipelineCostTracker _tracker;
    private readonly DateTimeOffset _startedAt;
    private bool _disposed;

    private LimitEnforcer? _enforcer;

    public SkillCallScope(string skillName, string role, SkillExecutionPhase phase,
        PipelineCostTracker tracker, DateTimeOffset startedAt)
    {
        SkillName = skillName;
        Role = role;
        Phase = phase;
        _tracker = tracker;
        _startedAt = startedAt;
    }

    public string SkillName { get; }
    public string Role { get; }
    public SkillExecutionPhase Phase { get; }
    public DateTimeOffset StartedAt => _startedAt;
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long CacheCreateTokens { get; private set; }
    public long CacheReadTokens { get; private set; }

    public void AddTokens(long input, long output, long cacheCreate, long cacheRead)
    {
        InputTokens += input;
        OutputTokens += output;
        CacheCreateTokens += cacheCreate;
        CacheReadTokens += cacheRead;
    }

    public void Finalize(LimitEnforcer enforcer) => _enforcer = enforcer;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tracker.EndCall(this, _enforcer);
    }

    internal CallCostRecord BuildRecord(LimitEnforcer? enforcer)
    {
        var elapsed = enforcer?.ElapsedMs ?? (long)(DateTimeOffset.UtcNow - _startedAt).TotalMilliseconds;
        return new CallCostRecord
        {
            SkillName = SkillName,
            Role = Role,
            Phase = Phase,
            InputTokens = InputTokens,
            OutputTokens = OutputTokens,
            CacheCreateTokens = CacheCreateTokens,
            CacheReadTokens = CacheReadTokens,
            ToolCallCount = enforcer?.ToolCallCount ?? 0,
            LlmCallCount = enforcer?.LlmCallCount ?? 0,
            DurationMs = elapsed,
            StartedAt = _startedAt
        };
    }
}
