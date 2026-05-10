using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Deterministic pre-filter for the triage candidate skill set. Walks each
/// skill's <c>activates_when</c> expression and includes only those that
/// evaluate true against the run-state concepts. Skills with no expression
/// pass through (the LLM-driven legacy <c>activation.positive</c> path
/// handles them downstream until p0127c migrates every skill).
/// </summary>
public sealed class ActivationSkillFilter(
    ActivationExpressionParser parser,
    ActivationEvaluator evaluator,
    ILogger<ActivationSkillFilter> logger)
{
    public IReadOnlyList<RoleSkillDefinition> Filter(
        IReadOnlyList<RoleSkillDefinition> skills, IRunStateConcepts state)
    {
        var cache = new Dictionary<string, ActivationExpression>(StringComparer.Ordinal);
        var kept = new List<RoleSkillDefinition>(skills.Count);
        foreach (var skill in skills)
        {
            if (Includes(skill, state, cache))
                kept.Add(skill);
        }
        return kept;
    }

    private bool Includes(
        RoleSkillDefinition skill, IRunStateConcepts state,
        Dictionary<string, ActivationExpression> cache)
    {
        if (string.IsNullOrWhiteSpace(skill.ActivatesWhen)) return true;
        if (!TryGetExpression(skill, cache, out var expression)) return false;
        try { return evaluator.Evaluate(expression, state); }
        catch (ActivationExpressionEvaluateException ex)
        {
            logger.LogError(
                "Skill '{Skill}' activates_when evaluate failed: {Message}", skill.Name, ex.Message);
            return false;
        }
    }

    private bool TryGetExpression(
        RoleSkillDefinition skill, Dictionary<string, ActivationExpression> cache,
        out ActivationExpression expression)
    {
        var raw = skill.ActivatesWhen!;
        if (cache.TryGetValue(raw, out var cached)) { expression = cached; return true; }
        try { expression = parser.Parse(raw); cache[raw] = expression; return true; }
        catch (ActivationExpressionParseException ex)
        {
            logger.LogError(
                "Skill '{Skill}' activates_when parse failed at offset {Offset}: {Message}",
                skill.Name, ex.Offset, ex.Message);
            expression = default!;
            return false;
        }
    }
}
