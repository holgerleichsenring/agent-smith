using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the caps are the schema's contract. A rejected spec goes BACK to the
/// model with these messages (the ExpectationDraftValidator pattern) rather than
/// being silently truncated, so an over-long spec is a visible failure of the
/// transform and not a quiet loss of a requirement.
/// </summary>
public sealed class WorkSpecValidator
{
    public IReadOnlyList<string> Validate(WorkSpec spec, string? samplesMarkdown)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(spec.Goal)) errors.Add("goal is required");
        if (spec.Requirements.Count == 0 && !spec.IsHandedBack)
            errors.Add("requirements must not be empty unless the ticket is handed back");
        Cap(errors, "requirements", spec.Requirements.Count, WorkSpec.MaxRequirements);
        Cap(errors, "constraints", spec.Constraints.Count, WorkSpec.MaxConstraints);
        Cap(errors, "assumptions", spec.Assumptions.Count, WorkSpec.MaxAssumptions);
        ValidateLengths(errors, spec);
        ValidateAnchors(errors, spec, samplesMarkdown);
        return errors;
    }

    private static void ValidateLengths(List<string> errors, WorkSpec spec)
    {
        foreach (var statement in spec.Requirements.Concat(spec.Done).Concat(spec.Assumptions))
            if (statement.Length > WorkSpec.MaxStatementLength)
                errors.Add(
                    $"\"{Preview(statement)}\" is {statement.Length} chars — over the "
                    + $"{WorkSpec.MaxStatementLength}-char statement cap; that is prose, not a statement");
    }

    // A constraint may reference a sample, but the referenced anchor must exist:
    // a dangling anchor means the rule lost its sample and the master would work
    // from half a contract.
    private static void ValidateAnchors(List<string> errors, WorkSpec spec, string? samplesMarkdown)
    {
        var anchors = WorkSpecSampleAnchors.Parse(samplesMarkdown);
        foreach (var constraint in spec.Constraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.SampleAnchor)) continue;
            if (!anchors.Contains(constraint.SampleAnchor))
                errors.Add(
                    $"constraint \"{Preview(constraint.Rule)}\" references sample anchor "
                    + $"'{constraint.SampleAnchor}', which spec.md does not define "
                    + $"(add a heading '## {WorkSpecSampleAnchors.HeadingPrefix}{constraint.SampleAnchor}')");
        }
    }

    private static void Cap(List<string> errors, string name, int count, int max)
    {
        if (count > max) errors.Add($"{name} has {count} entries — the cap is {max}");
    }

    private static string Preview(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";
}
