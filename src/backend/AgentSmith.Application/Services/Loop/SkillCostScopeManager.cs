using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Tracks the active <see cref="SkillCallScope"/> for a pipeline run and the list
/// of finalized <see cref="CallCostRecord"/>s. Skill calls don't nest; opening a
/// second scope while one is active throws to surface the bug.
/// <para>2026-11-02-2b26b: "active" is per async flow, so concurrent calls on one tracker — the
/// parallel attack reviewers — each attribute to their own scope. The AsyncLocal is per instance:
/// a static one would share a scope across every tracker in a flow.</para>
/// </summary>
public sealed class SkillCostScopeManager
{
    private readonly object _gate = new();
    private readonly List<CallCostRecord> _records = new();
    private readonly AsyncLocal<SkillCallScope?> _activeScope = new();

    public IReadOnlyList<CallCostRecord> PerSkillBreakdown
    {
        get { lock (_gate) return _records.OrderBy(r => r.StartedAt).ToArray(); }
    }

    public SkillCallScope BeginCall(
        string skillName, string role, SkillExecutionPhase phase, PipelineCostTracker tracker,
        string? repoName = null)
    {
        if (Active is not null)
            throw new InvalidOperationException(
                "skill calls do not nest — a SkillCallScope is already active");

        var scope = new SkillCallScope(skillName, role, phase, tracker, DateTimeOffset.UtcNow, repoName);
        _activeScope.Value = scope;
        return scope;
    }

    public void EndCall(SkillCallScope scope, LimitEnforcer? enforcer)
    {
        if (!ReferenceEquals(_activeScope.Value, scope))
            return;
        _activeScope.Value = null;
        lock (_gate) _records.Add(scope.BuildRecord(enforcer));
    }

    public void AttributeTokens(long input, long output, long cacheCreate, long cacheRead)
    {
        var scope = Active;
        if (scope is null) return;
        lock (_gate) scope.AddTokens(input, output, cacheCreate, cacheRead);
    }

    /// <summary>p0361: attributes one call's own-model USD to the active scope,
    /// so phase costs are exact instead of re-priced at the run's last model.</summary>
    public void AttributeCost(string model, decimal usd)
    {
        var scope = Active;
        if (scope is null) return;
        lock (_gate) scope.AddCost(model, usd);
    }

    // A child flow forked while a scope was open still holds it after the scope ended; an ended scope is never active.
    private SkillCallScope? Active => _activeScope.Value is { IsEnded: false } scope ? scope : null;
}
