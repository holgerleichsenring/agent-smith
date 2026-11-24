using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the GitOps commit step and the render it runs; without a database no Poc row exists.</summary>
public static class PocGitOpsExtensions
{
    public static IServiceCollection AddPocGitOps(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocGitOpsStore, NoPocGitOpsStore>();
        return services.AddTransient<PocContainerFields>().AddTransient<PocDeploymentFields>().AddTransient<PocIngressFields>()
            .AddTransient<PocConfigMapRender>().AddTransient<PocPodPolicy>().AddTransient<PocManifestPolicy>()
            .AddTransient<PocAppRender>().AddTransient<PocTemplateRead>().AddTransient<PocFolderPlan>()
            .AddTransient<PocGitOpsPlanner>().AddTransient<PocDesiredStates>().AddTransient<PocRenderInputs>()
            .AddTransient<PocDeployedPublisher>().AddTransient<PocGitOpsCommit>()
            .AddTransient<PocAppTemplateDefaults>().AddTransient<PocAppTemplateWrite>()
            .AddTransient<ICommandHandler<GitOpsCommitContext>, GitOpsCommitHandler>();
    }
}
