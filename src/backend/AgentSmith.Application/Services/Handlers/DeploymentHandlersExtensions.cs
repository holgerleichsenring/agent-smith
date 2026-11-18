using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-18-3863c: the deploy preset's step and the services it orchestrates.</summary>
public static class DeploymentHandlersExtensions
{
    public static IServiceCollection AddDeploymentHandlers(this IServiceCollection services) =>
        services.AddTransient<DeploymentDeclarationParser>().AddTransient<ManifestVersionKey>()
            .AddTransient<DeploymentManifest>().AddTransient<DeployVersion>().AddTransient<DeployCiStatus>()
            .AddTransient<DeploymentTemplateRead>().AddTransient<DeployManifestPlan>().AddTransient<GitOpsDelivery>()
            .AddTransient<ICommandHandler<DeployToEnvironmentContext>, DeployToEnvironmentHandler>();
}
