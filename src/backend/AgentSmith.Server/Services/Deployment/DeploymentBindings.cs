using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>2026-11-18-3863c: a project's deployment bindings, read from config for the dashboard's deploy act.</summary>
public sealed class DeploymentBindings(IConfigurationLoader configLoader, ServerContext serverContext)
{
    /// <summary>The bindings, or null when no such project is configured.</summary>
    public IReadOnlyList<DeploymentBindingView>? Of(string projectName)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project)) return null;
        return [.. project.Templates.Where(t => t.IsDeployment).Select(View)];
    }

    private static DeploymentBindingView View(ProjectTemplate binding) => new(
        binding.Context, binding.ContextRepo,
        $"{binding.Repo.Name} · {binding.TemplateContext} @ {binding.Revision ?? "its default branch"}",
        [.. (binding.Environments ?? []).Select(e => new DeploymentEnvironmentView(e.Name, e.Approval, e.Repo?.Name))]);
}
