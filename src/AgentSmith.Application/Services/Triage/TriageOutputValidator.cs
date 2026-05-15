using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Two-layer validation for a TriageOutput:
/// (1) structural — rationale length cap, no newlines, role/phase coherence per PhaseAssignment;
/// (2) semantic — every assigned skill exists in the available-skills catalog and its
///     <c>role</c> matches the slot it was assigned to (producer→Lead, investigator→
///     Analyst, judge→Reviewer, filter→Filter); rationale-cited keys reference the
///     global concept vocabulary.
/// p0131a: shape simplified — legacy ActivationCriteria-bag + RoleAssignments removed.
/// activates_when is the activation contract; the rationale-key check now goes
/// vocabulary-only (skill-specific keys lived in the legacy criteria bag).
/// </summary>
public sealed class TriageOutputValidator(TriageRationaleParser rationaleParser)
{
    // 1000 (raised from 500) gives the LLM headroom for security-scan / api-security-scan
    // catalogs (15-20 skills) where each phase slot carries its own positive justification
    // and the rejected-candidate negative entries add another bracket of tokens. Observed
    // overshoots on the 500-cap clustered around 600-800 chars — 1000 covers them with
    // margin while still keeping the JSON rationale field operator-readable on one screen.
    private const int MaxRationaleChars = 1000;

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
        var supported = SkillRoleMapping.ToSkillRole(skill.Role);
        if (supported != role)
            errors.Add(
                $"Phase {phase}: skill '{skillName}' assigned slot {role} but its role is " +
                $"'{skill.Role}' (maps to {supported})");
    }

    private void ValidateRationaleKeys(
        string rationale,
        IReadOnlyDictionary<string, SkillIndexEntry> skills,
        ConceptVocabulary vocabulary,
        List<string> errors)
    {
        foreach (var entry in rationaleParser.Parse(rationale))
        {
            if (!skills.ContainsKey(entry.Skill))
            {
                errors.Add($"Rationale references unknown skill '{entry.Skill}'");
                continue;
            }
            // p0131a: legacy ActivationCriteria-bag retired. Rationale keys are now
            // checked against the global concept vocabulary only — the per-skill
            // criteria store is gone post-p0127c.
            if (!vocabulary.TryGet(entry.Key, out _))
                errors.Add($"Rationale: cited key '{entry.Key}' is not declared in the concept vocabulary");
        }
    }
}
