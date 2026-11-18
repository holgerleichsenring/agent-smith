using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-3863a: what a binding's kind and environments may not be. Read from <see cref="ProjectTemplateRules"/>,
/// so the loader and the studio write state it once. Whether an environment's repo RESOLVES is the loader's to say: it
/// resolves the ref against the repos catalog the way a template's own repo is resolved.
/// </summary>
public static class DeploymentEnvironmentRules
{
    /// <summary>One message per broken rule of <paramref name="template"/>, empty when it is sound.</summary>
    public static IReadOnlyList<string> Check(string project, TemplateReference template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var prefix = $"project '{project}': template for context '{template.Context}'";
        if (!TemplateKinds.IsKnown(template.Kind))
            return [$"{prefix} has kind '{template.Kind}'; a kind is '{TemplateKinds.Code}' or '{TemplateKinds.Deployment}'"];
        var environments = template.Environments ?? [];
        if (environments.Count == 0) return [];
        if (!TemplateKinds.IsDeployment(template.Kind))
            return [$"{prefix} declares environments but is not of kind '{TemplateKinds.Deployment}'"];
        return EnvironmentErrors(prefix, environments);
    }

    private static List<string> EnvironmentErrors(string prefix, IReadOnlyList<DeploymentEnvironmentReference> environments)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(ConfigNames.Comparer);
        foreach (var environment in environments)
        {
            if (string.IsNullOrWhiteSpace(environment.Name))
                errors.Add($"{prefix} has an environment without a name");
            else if (!seen.Add(environment.Name))
                errors.Add($"{prefix} names environment '{environment.Name}' twice");
            if (environment.Repo?.Contains('*', StringComparison.Ordinal) == true)
                errors.Add($"{prefix}: environment '{environment.Name}' names glob repo ref '{environment.Repo}' — "
                    + "an environment's manifest lives in one repository");
        }
        return errors;
    }
}
