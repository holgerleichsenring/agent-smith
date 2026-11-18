using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: the deployment binding and bound environment a request names, out of the project's config. Shared
/// by the server, which refuses an unbound request before it queues a run, and the run, which acts on it.
/// </summary>
public sealed record DeploymentTarget(ProjectTemplate Binding, DeploymentEnvironment Environment)
{
    /// <summary>The target, or null with the sentence that says what is missing.</summary>
    public static (DeploymentTarget? Target, string? Error) Of(ResolvedProject project, DeployRequest request)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var bindings = project.Templates.Where(t => t.IsDeployment && Same(t.Context, request.Context)
            && (request.ContextRepo is null || Same(t.ContextRepo, request.ContextRepo))).ToList();
        if (bindings.Count == 0)
            return (null, $"project '{project.Name}' binds no deployment template for context '{request.Context}'");
        if (bindings.Count > 1)
            return (null, $"project '{project.Name}' binds context '{request.Context}' to a deployment template in "
                + "more than one repository; name the context's repository");
        var environment = Bound(bindings[0], request.Environment);
        return environment is null
            ? (null, $"context '{request.Context}' of project '{project.Name}' binds no environment '{request.Environment}'")
            : (new DeploymentTarget(bindings[0], environment), null);
    }

    /// <summary>The repository holding <paramref name="environment"/>'s manifest: its bound repo, else the context's own —
    /// the binding's context_repo, else the project's only repository.</summary>
    public (RepoConnection? Repo, string? Error) RepoOf(ResolvedProject project, DeploymentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.Repo is not null) return (environment.Repo, null);
        var own = Binding.ContextRepo is { } named
            ? project.Repos.FirstOrDefault(r => Same(r.Name, named) || Same(r.Name, named.Split('/')[^1]))
            : project.Repos.Count == 1 ? project.Repos[0] : null;
        return own is not null
            ? (own, null)
            : (null, $"environment '{environment.Name}' names no repo and the repository of context '{Binding.Context}' "
                + "is not one; give the environment a repo, or the binding a context_repo");
    }

    /// <summary>The environment named <paramref name="name"/> the binding carries, or null.</summary>
    public static DeploymentEnvironment? Bound(ProjectTemplate binding, string name) =>
        (binding.Environments ?? []).FirstOrDefault(e => Same(e.Name, name));

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
