using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>2026-11-23-933dd: the build step of a poc-deploy run, the poc-target-install step and the build pipeline
/// they write, with the no-database default of the build store.</summary>
public static class PocBuildExtensions
{
    public static IServiceCollection AddPocBuild(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocBuildStore, NoPocBuildStore>();
        services.AddSingleton<PocPipelineResources>().AddSingleton<PocYamlFill>().AddSingleton<PocPipelineScripts>()
            .AddSingleton<GitHubPocWorkflow>().AddSingleton<AzurePocPipeline>().AddSingleton<GitLabPocCi>()
            .AddSingleton<PocPipelineTemplate>().AddTransient<PocSourceRepos>().AddTransient<PocPipelineWrite>()
            .AddTransient<ICommandHandler<PocTargetInstallContext>, PocTargetInstallHandler>();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddSingleton<PocTrivySecretRules>().AddSingleton<TrivyReportParser>().AddSingleton<PocImageRules>()
            .AddSingleton<PocTrivyIgnore>().AddSingleton<PocBuildInputs>().AddTransient<PocBuildReport>()
            .AddTransient<PocBuildRegistry>().AddTransient<PocBuildStart>().AddTransient<PocBuildFollow>()
            .AddTransient<PocBuildOverrule>().AddTransient<PocBuildOutcome>().AddTransient<PocBuildRun>()
            .AddTransient<ICommandHandler<PocPipelineBuildContext>, PocPipelineBuildHandler>();
    }
}
