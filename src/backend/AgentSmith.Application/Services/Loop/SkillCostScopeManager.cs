using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Tracks the active <see cref="SkillCallScope"/> for a pipeline run and the list
/// of finalized <see cref="CallCostRecord"/>s. Skill calls don't nest; opening a
/// second scope while one is active throws to surface the bug.
/// </summary>
public sealed class SkillCostScopeManager
{
    private readonly object _gate = new();
    private readonly List<CallCostRecord> _records = new();
    private SkillCallScope? _activeScope;

    public IReadOnlyList<CallCostRecord> PerSkillBreakdown
    {
        get { lock (_gate) return _records.OrderBy(r => r.StartedAt).ToArray(); }
    }

    public SkillCallScope BeginCall(
        string skillName, string role, SkillExecutionPhase phase, PipelineCostTracker tracker,
        string? repoName = null)
    {
        lock (_gate)
        {
            if (_activeScope is not null)
                throw new InvalidOperationException(
                    "skill calls do not nest — a SkillCallScope is already active");

            var scope = new SkillCallScope(skillName, role, phase, tracker, DateTimeOffset.UtcNow, repoName);
            _activeScope = scope;
            return scope;
        }
    }

    public void EndCall(SkillCallScope scope, LimitEnforcer? enforcer)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_activeScope, scope))
                return;
            _records.Add(scope.BuildRecord(enforcer));
            _activeScope = null;
        }
    }

    public void AttributeTokens(long input, long output, long cacheCreate, long cacheRead)
    {
        lock (_gate)
        {
            _activeScope?.AddTokens(input, output, cacheCreate, cacheRead);
        }
    }

    /// <summary>p0361: attributes one call's own-model USD to the active scope,
    /// so phase costs are exact instead of re-priced at the run's last model.</summary>
    public void AttributeCost(string model, decimal usd)
    {
        lock (_gate)
        {
            _activeScope?.AddCost(model, usd);
        }
    }
}
