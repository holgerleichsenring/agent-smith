using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

/// <summary>
/// Registers all application services (commands, handlers, pipeline, use cases) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCommands(this IServiceCollection services)
    {
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        RegisterHandlers(services);
        RegisterPipeline(services);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        services.AddTransient<ICommandHandler<CheckoutSourceContext>, CheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<LoadDomainRulesContext>, LoadDomainRulesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeCodeHandler>();
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<ICommandHandler<TestContext>, TestHandler>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<BootstrapProjectContext>, BootstrapProjectHandler>();
        services.AddTransient<ICommandHandler<LoadCodeMapContext>, LoadCodeMapHandler>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<TriageContext>, TriageHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<ICommandHandler<SkillRoundContext>, SkillRoundHandler>();
        services.AddTransient<ICommandHandler<ConvergenceCheckContext>, ConvergenceCheckHandler>();
        services.AddTransient<ICommandHandler<GenerateTestsContext>, GenerateTestsHandler>();
        services.AddTransient<ICommandHandler<GenerateDocsContext>, GenerateDocsHandler>();
    }

    private static void RegisterPipeline(IServiceCollection services)
    {
        services.AddTransient<IIntentParser, RegexIntentParser>();
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        services.AddTransient<IPipelineExecutor, PipelineExecutor>();
        services.AddTransient<ProcessTicketUseCase>();
    }
}
