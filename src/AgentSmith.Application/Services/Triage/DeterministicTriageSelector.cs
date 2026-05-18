using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0143: pure-function deterministic triage. Inputs are the activates_when-
/// filtered candidate list (narrowed by ActivationSkillFilter) and the
/// pipeline phases to fill. For each phase, fills the slots from the
/// candidate pool based on each candidate's SKILL.md <c>role</c>:
/// <list type="bullet">
///   <item>Plan needs a Lead (producer) + Analysts (investigator) + optional Filter.</item>
///   <item>Review needs a Lead (producer — same producer re-runs) + Reviewers (judge).</item>
///   <item>Final needs a Filter (filter).</item>
/// </list>
/// Single-slot picks (Lead, Filter) take the highest-specificity candidate
/// with alphabetical-by-name as the deterministic tiebreak; multi-slot
/// (Analysts, Reviewers) include all matching candidates ordered by
/// specificity then name. Replaces the LLM-driven TriageOutputProducer
/// call — by construction the output references only known skills with
/// roles matching their slots.
/// </summary>
public sealed class DeterministicTriageSelector
{
    private static readonly IReadOnlyList<PipelinePhase> DefaultPhases =
        [PipelinePhase.Plan, PipelinePhase.Review, PipelinePhase.Final];

    private readonly ActivationSpecificityScorer _scorer;

    public DeterministicTriageSelector(ActivationSpecificityScorer scorer) => _scorer = scorer;

    public TriageOutput Select(IReadOnlyList<RoleSkillDefinition> filteredSkills)
    {
        var phases = new Dictionary<PipelinePhase, PhaseAssignment>(DefaultPhases.Count);
        foreach (var phase in DefaultPhases)
            phases[phase] = AssignPhase(phase, filteredSkills);
        var rationale = BuildRationale(filteredSkills);
        return new TriageOutput(phases, Confidence: 100, rationale);
    }

    private PhaseAssignment AssignPhase(
        PipelinePhase phase, IReadOnlyList<RoleSkillDefinition> filtered)
    {
        return phase switch
        {
            PipelinePhase.Plan => new PhaseAssignment(
                Lead: PickBest(filtered, "producer"),
                Analysts: PickAll(filtered, "investigator"),
                Reviewers: Array.Empty<string>(),
                Filter: null),
            PipelinePhase.Review => new PhaseAssignment(
                Lead: PickBest(filtered, "producer"),
                Analysts: Array.Empty<string>(),
                Reviewers: PickAll(filtered, "judge"),
                Filter: null),
            PipelinePhase.Final => new PhaseAssignment(
                Lead: null,
                Analysts: Array.Empty<string>(),
                Reviewers: Array.Empty<string>(),
                Filter: PickBest(filtered, "filter")),
            _ => EmptyAssignment()
        };
    }

    private static PhaseAssignment EmptyAssignment() =>
        new(null, Array.Empty<string>(), Array.Empty<string>(), null);

    private string? PickBest(IReadOnlyList<RoleSkillDefinition> filtered, string role)
        => filtered
            .Where(s => string.Equals(s.Role, role, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => _scorer.Score(s.ActivatesWhen))
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => s.Name)
            .FirstOrDefault();

    private IReadOnlyList<string> PickAll(IReadOnlyList<RoleSkillDefinition> filtered, string role)
        => filtered
            .Where(s => string.Equals(s.Role, role, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => _scorer.Score(s.ActivatesWhen))
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => s.Name)
            .ToList();

    private static string BuildRationale(IReadOnlyList<RoleSkillDefinition> filtered)
    {
        if (filtered.Count == 0) return "deterministic-triage: no candidates after activates_when filter";
        var byRole = filtered
            .GroupBy(s => s.Role ?? "(none)", StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Count()}");
        return $"deterministic-triage: candidates by role [{string.Join(", ", byRole)}]";
    }
}
