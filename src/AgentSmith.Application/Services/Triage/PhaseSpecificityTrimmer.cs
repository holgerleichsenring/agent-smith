using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Trims each phase of a <see cref="TriageOutput"/> to the per-phase skill cap,
/// dropping the lowest-specificity skills first. Specificity is the
/// <c>activates_when</c> AST term count via
/// <see cref="ActivationSpecificityScorer"/>; ties broken by skill name
/// ascending so the output is deterministic across runs.
/// </summary>
public sealed class PhaseSpecificityTrimmer(
    ActivationSpecificityScorer scorer, ILogger<PhaseSpecificityTrimmer> logger)
{
    public TriageOutput Trim(
        TriageOutput output, IReadOnlyList<RoleSkillDefinition> skills, int cap)
    {
        var scoreByName = BuildScoreIndex(skills);
        var phases = new Dictionary<PipelinePhase, PhaseAssignment>(output.Phases.Count);
        foreach (var (phase, assignment) in output.Phases)
            phases[phase] = TrimPhase(phase, assignment, scoreByName, cap);
        return output with { Phases = phases };
    }

    private Dictionary<string, int> BuildScoreIndex(IReadOnlyList<RoleSkillDefinition> skills)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var skill in skills)
            index[skill.Name] = scorer.Score(skill.ActivatesWhen);
        return index;
    }

    private PhaseAssignment TrimPhase(
        PipelinePhase phase, PhaseAssignment assignment,
        Dictionary<string, int> scoreByName, int cap)
    {
        var total = TotalSkillCount(assignment);
        if (total <= cap) return assignment;
        var fixedCount = (assignment.Lead is null ? 0 : 1) + (assignment.Filter is null ? 0 : 1);
        var listCap = Math.Max(0, cap - fixedCount);
        var keptAnalysts = TrimList(assignment.Analysts, scoreByName, listCap);
        var keptReviewers = TrimList(
            assignment.Reviewers, scoreByName, Math.Max(0, listCap - keptAnalysts.Count));
        var trimmed = new PhaseAssignment(
            assignment.Lead, keptAnalysts, keptReviewers, assignment.Filter);
        logger.LogInformation(
            "Phase {Phase} trimmed from {Before} to {After} skills by specificity (kept: {Names})",
            phase, total, TotalSkillCount(trimmed),
            string.Join(", ", EnumerateNames(trimmed)));
        return trimmed;
    }

    private static IReadOnlyList<string> TrimList(
        IReadOnlyList<string> names, Dictionary<string, int> scoreByName, int cap) =>
        names
            .OrderByDescending(n => scoreByName.TryGetValue(n, out var s) ? s : 0)
            .ThenBy(n => n, StringComparer.Ordinal)
            .Take(cap)
            .ToList();

    private static int TotalSkillCount(PhaseAssignment a) =>
        (a.Lead is null ? 0 : 1) + a.Analysts.Count + a.Reviewers.Count + (a.Filter is null ? 0 : 1);

    private static IEnumerable<string> EnumerateNames(PhaseAssignment a)
    {
        if (a.Lead is not null) yield return a.Lead;
        foreach (var n in a.Analysts) yield return n;
        foreach (var n in a.Reviewers) yield return n;
        if (a.Filter is not null) yield return a.Filter;
    }
}
