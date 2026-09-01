using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Infrastructure.Services.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the boundaries a SCORED scan puts back.
/// <para>
/// The harness stands four things down that a preset test does not need and a measurement
/// cannot do without: the model, the skills catalog the master and the pattern scanner
/// read, the prompt catalog that serves the master's body, and the refutation the delivered
/// findings pass through. Each one left stubbed turns the score into a score of the stub —
/// with a scripted client the tier would be measuring the scanners, not the scan.
/// </para>
/// <para>
/// The sandbox becomes the CLI-mode in-process one, over the corpus tree. That is the
/// sandbox an operator's <c>agentsmith</c> run uses, and it is the only backend that reads
/// a host directory as <c>/work</c> without a container.
/// </para>
/// </summary>
internal static class ScanEvalComposition
{
    /// <summary>Every boundary a repository scan is measured across. The agent itself is
    /// the caller's — see <see cref="AgentCliProbe.Agent"/>.</summary>
    internal static Action<IServiceCollection> DrivenByAgentCli() => services =>
    {
        RestoreModel(services);
        RestoreCatalog(services);
        RestoreRefutation(services);
        RestoreSandbox(services);
    };

    /// <summary>Additionally the swagger read, for a scan whose subject is a SERVED
    /// document: the harness's stub answers one invented endpoint whatever it is asked,
    /// so a served document would never reach the master.</summary>
    internal static Action<IServiceCollection> DrivenByAgentCliAgainstAServedTarget() => services =>
    {
        DrivenByAgentCli()(services);
        services.RemoveAll<ISwaggerProvider>();
        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();
    };

    // The PRODUCTION factory, so the run selects its client exactly as a deployed server
    // does — from the agent's declared type — instead of being handed one.
    private static void RestoreModel(IServiceCollection services)
    {
        services.RemoveAll<IChatClientFactory>();
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
    }

    private static void RestoreCatalog(IServiceCollection services)
    {
        services.RemoveAll<ISkillsCatalogPath>();
        services.AddSingleton<ISkillsCatalogPath, EmbeddedSkillsCatalogPath>();
        services.RemoveAll<IPromptCatalog>();
        services.AddPromptCatalog();
    }

    // A stood-down refuter delivers every candidate the scanners raised, which is louder
    // than any run ships. The score is of what a run DELIVERS, so the real one runs.
    private static void RestoreRefutation(IServiceCollection services)
    {
        services.RemoveAll<IFindingRefuter>();
        services.AddTransient<IFindingRefuter, FindingRefuter>();
    }

    private static void RestoreSandbox(IServiceCollection services)
    {
        services.RemoveAll<ISandboxFactory>();
        services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>();
    }
}
