using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Two-layer validation for a TriageOutput:
/// (1) structural — rationale length cap, no newlines, role/phase coherence per PhaseAssignment;
/// (2) semantic — every assigned skill supports the assigned role; every rationale token
/// references either a key declared in the cited skill's activation/role_assignment OR
/// any key declared in the global concept vocabulary. The skill-specific keys remain
/// useful as priority signals to the triage prompt (highest-fit skill picks), but the
/// validator no longer requires the LLM's chosen key to come from that narrow whitelist.
/// </summary>
public sealed class TriageOutputValidator(TriageRationaleParser rationaleParser)
{
    private const int MaxRationaleChars = 500;

    public TriageValidationResult Validate(
        TriageOutput output,
        IReadOnlyList<SkillIndexEntry> availableSkills,
        ConceptVocabulary? vocabulary = null)
    {
        var errors = new List<string>();
        ValidateRationaleStructure(output.Rationale, errors);
        var skillsByName = availableSkills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        ValidateAssignments(output.Phases, skillsByName, errors);
        ValidateRationaleKeys(output.Rationale, skillsByName, vocabulary ?? ConceptVocabulary.Empty, errors);
        return errors.Count == 0
            ? TriageValidationResult.Ok
            : new TriageValidationResult(false, errors);
    }

    private static void ValidateRationaleStructure(string rationale, List<string> errors)
    {
        if (rationale.Length > MaxRationaleChars)
            errors.Add($"Rationale exceeds {MaxRationaleChars} chars (got {rationale.Length})");
        if (rationale.Contains('\n') || rationale.Contains('\r'))
            errors.Add("Rationale contains newlines (forbidden)");
    }

    private static void ValidateAssignments(
        IReadOnlyDictionary<PipelinePhase, PhaseAssignment> phases,
        IReadOnlyDictionary<string, SkillIndexEntry> skills,
        List<string> errors)
    {
        foreach (var (phase, assignment) in phases)
        {
            CheckRoleSupported(assignment.Lead, SkillRole.Lead, phase, skills, errors);
            foreach (var name in assignment.Analysts)
                CheckRoleSupported(name, SkillRole.Analyst, phase, skills, errors);
            foreach (var name in assignment.Reviewers)
                CheckRoleSupported(name, SkillRole.Reviewer, phase, skills, errors);
            CheckRoleSupported(assignment.Filter, SkillRole.Filter, phase, skills, errors);
        }
    }

    private static void CheckRoleSupported(
        string? skillName, SkillRole role, PipelinePhase phase,
        IReadOnlyDictionary<string, SkillIndexEntry> skills, List<string> errors)
    {
        if (skillName is null) return;
        if (!skills.TryGetValue(skillName, out var skill))
        {
            errors.Add($"Phase {phase}: skill '{skillName}' not in available skills");
            return;
        }
        if (!skill.RolesSupported.Contains(role))
            errors.Add($"Phase {phase}: skill '{skillName}' assigned role {role} not in roles_supported");
    }

    private void ValidateRationaleKeys(
        string rationale,
        IReadOnlyDictionary<string, SkillIndexEntry> skills,
        ConceptVocabulary vocabulary,
        List<string> errors)
    {
        foreach (var entry in rationaleParser.Parse(rationale))
        {
            if (!skills.TryGetValue(entry.Skill, out var skill))
            {
                errors.Add($"Rationale references unknown skill '{entry.Skill}'");
                continue;
            }
            if (!HasKey(skill, entry.Key, entry.Role, vocabulary))
                errors.Add($"Rationale: skill '{entry.Skill}' has no key '{entry.Key}'" +
                           (entry.Role.HasValue ? $" for role {entry.Role}" : ""));
        }
    }

    private static bool HasKey(SkillIndexEntry skill, string key, SkillRole? role, ConceptVocabulary vocabulary)
    {
        if (skill.Activation.Positive.Any(k => k.Key == key)) return true;
        if (skill.Activation.Negative.Any(k => k.Key == key)) return true;
        if (role.HasValue)
        {
            var roleAssignment = skill.RoleAssignments.FirstOrDefault(r => r.Role == role.Value);
            if (roleAssignment is not null
                && (roleAssignment.Criteria.Positive.Any(k => k.Key == key)
                    || roleAssignment.Criteria.Negative.Any(k => k.Key == key)))
                return true;
        }

        // Defensive default: when a skill defines NO activation criteria and NO role-assignment
        // criteria at all, treat any cited key as acceptable. The skill author hasn't expressed
        // constraints, so the LLM has no valid keys to cite — rejecting them would block triage
        // on a skill-metadata gap, not a real triage error.
        if (HasNoCriteriaAtAll(skill)) return true;

        // Vocab-fallback: any key declared in the global concept vocabulary is accepted as a
        // valid rationale even when the skill author didn't list it in role_assignment.<role>
        // .positive. The skill-specific keys remain useful as priority signals to the triage
        // prompt (LLM picks the BEST-FIT skill given run context), but they're no longer a
        // closed enum the LLM must memorize. A vocab key is a known concept; if the LLM picks
        // it as rationale, it's semantically valid. Without this, vocabulary-skill drift
        // breaks triage hard — see p0124 / agent-smith-skills 1.7.1 incident.
        return vocabulary.TryGet(key, out _);
    }

    private static bool HasNoCriteriaAtAll(SkillIndexEntry skill) =>
        skill.Activation.Positive.Count == 0
        && skill.Activation.Negative.Count == 0
        && skill.RoleAssignments.All(r =>
            r.Criteria.Positive.Count == 0 && r.Criteria.Negative.Count == 0);
}
