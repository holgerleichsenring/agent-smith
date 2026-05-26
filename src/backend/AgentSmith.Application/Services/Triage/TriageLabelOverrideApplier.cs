using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Applies ticket-label hard overrides to a TriageOutput. Two label patterns are recognised:
/// <c>agent-smith:skip:&lt;skill&gt;</c> drops a specific skill from all roles in all phases;
/// <c>agent-smith:no-test-adaption</c> drops the conventional <c>tester</c> skill.
/// </summary>
public sealed class TriageLabelOverrideApplier
{
    private const string SkipPrefix = "agent-smith:skip:";
    private const string NoTestAdaptionLabel = "agent-smith:no-test-adaption";
    private const string TesterSkill = "tester";

    public TriageOutput Apply(TriageOutput output, IReadOnlyList<string> labels)
    {
        var skillsToRemove = CollectSkillsToRemove(labels);
        if (skillsToRemove.Count == 0) return output;

        var newPhases = new Dictionary<PipelinePhase, PhaseAssignment>();
        foreach (var (phase, assignment) in output.Phases)
            newPhases[phase] = StripSkills(assignment, skillsToRemove);
        return output with { Phases = newPhases };
    }

    private static HashSet<string> CollectSkillsToRemove(IReadOnlyList<string> labels)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in labels)
        {
            if (label.StartsWith(SkipPrefix, StringComparison.OrdinalIgnoreCase))
                set.Add(label[SkipPrefix.Length..]);
            else if (string.Equals(label, NoTestAdaptionLabel, StringComparison.OrdinalIgnoreCase))
                set.Add(TesterSkill);
        }
        return set;
    }

    private static PhaseAssignment StripSkills(PhaseAssignment assignment, HashSet<string> remove)
    {
        var lead = assignment.Lead is not null && remove.Contains(assignment.Lead) ? null : assignment.Lead;
        var filter = assignment.Filter is not null && remove.Contains(assignment.Filter) ? null : assignment.Filter;
        var analysts = assignment.Analysts.Where(s => !remove.Contains(s)).ToList();
        var reviewers = assignment.Reviewers.Where(s => !remove.Contains(s)).ToList();
        return new PhaseAssignment(lead, analysts, reviewers, filter);
    }
}
