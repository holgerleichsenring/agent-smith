using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-18-3863b: what the coding master is told about an opened DEPLOYMENT template — which context it delivers,
/// where its declaration is, the environments this project binds with their parameter values, and the two rules that
/// make "make it deployable" ordinary work: a parameter nobody gave is asked, and a secret is referenced by name only.
/// </summary>
internal static class DeploymentTemplateDetail
{
    internal static string Of(ProjectTemplate declared, string? sha)
    {
        ArgumentNullException.ThrowIfNull(declared);
        var project = string.IsNullOrWhiteSpace(declared.Repo.Project) ? "" : $" (project `{declared.Repo.Project}`)";
        var revision = sha ?? declared.Revision ?? "its default revision";
        var declaration = $"{ProjectMetaPaths.Contexts}/{declared.TemplateContext}/{ProjectMetaPaths.DeploymentFile}";
        return $"the `{declared.TemplateContext}` context of `{declared.Repo.Name}`{project} at `{revision}`, saying how "
            + $"this project's `{declared.Context}` context is DELIVERED. Its declaration is `{declaration}` (files, "
            + "parameters, secrets, environments); the files it names are relative to that repository's root. "
            + $"Environments bound here: {Environments(declared.Environments ?? [])}. To make the context deployable, "
            + "copy or adapt the declared files into it, filling each parameter from these values or the template's "
            + "default; a parameter with neither is a question for a person, asked with the value you recommend. A "
            + "secret the declaration names is referenced by its name in the files and never written as a value";
    }

    private static string Environments(IReadOnlyList<DeploymentEnvironment> environments) =>
        environments.Count == 0
            ? "none yet"
            : string.Join("; ", environments.Select(Environment));

    private static string Environment(DeploymentEnvironment environment)
    {
        var repo = environment.Repo is null ? "manifest in this context's own repository" : $"manifest in `{environment.Repo.Name}`";
        var approval = environment.Approval ? "a person approves each delivery" : "delivered without a person's approval";
        var parameters = environment.Parameters.Count == 0
            ? "no parameter values"
            : "parameters " + string.Join(", ", environment.Parameters.Select(p => $"`{p.Key}`=`{p.Value}`"));
        return $"`{environment.Name}` ({repo}; {approval}; {parameters})";
    }
}
