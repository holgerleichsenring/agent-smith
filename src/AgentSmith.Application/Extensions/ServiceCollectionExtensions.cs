using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
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
        RegisterContextBuilders(services);
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
        services.AddTransient<ICommandHandler<CompileDiscussionContext>, CompileDiscussionHandler>();
        services.AddTransient<MetaFileBootstrapper>();
    }

    private static void RegisterContextBuilders(IServiceCollection services)
    {
        AddBuilder<FetchTicketContextBuilder>(services, CommandNames.FetchTicket);
        AddBuilder<CheckoutSourceContextBuilder>(services, CommandNames.CheckoutSource);
        AddBuilder<LoadDomainRulesContextBuilder>(services, CommandNames.LoadDomainRules);
        AddBuilder<LoadDomainRulesContextBuilder>(services, CommandNames.LoadCodingPrinciples);
        AddBuilder<LoadContextContextBuilder>(services, CommandNames.LoadContext);
        AddBuilder<LoadCodeMapContextBuilder>(services, CommandNames.LoadCodeMap);
        AddBuilder<BootstrapProjectContextBuilder>(services, CommandNames.BootstrapProject);
        AddBuilder<AnalyzeCodeContextBuilder>(services, CommandNames.AnalyzeCode);
        AddBuilder<GeneratePlanContextBuilder>(services, CommandNames.GeneratePlan);
        AddBuilder<ApprovalContextBuilder>(services, CommandNames.Approval);
        AddBuilder<AgenticExecuteContextBuilder>(services, CommandNames.AgenticExecute);
        AddBuilder<TestContextBuilder>(services, CommandNames.Test);
        AddBuilder<WriteRunResultContextBuilder>(services, CommandNames.WriteRunResult);
        AddBuilder<CommitAndPRContextBuilder>(services, CommandNames.CommitAndPR);
        AddBuilder<InitCommitContextBuilder>(services, CommandNames.InitCommit);
        AddBuilder<TriageContextBuilder>(services, CommandNames.Triage);
        AddBuilder<SwitchSkillContextBuilder>(services, CommandNames.SwitchSkill);
        AddBuilder<SkillRoundContextBuilder>(services, CommandNames.SkillRound);
        AddBuilder<ConvergenceCheckContextBuilder>(services, CommandNames.ConvergenceCheck);
        AddBuilder<GenerateTestsContextBuilder>(services, CommandNames.GenerateTests);
        AddBuilder<GenerateDocsContextBuilder>(services, CommandNames.GenerateDocs);
        AddBuilder<CompileDiscussionContextBuilder>(services, CommandNames.CompileDiscussion);
    }

    private static void AddBuilder<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));

    private static void RegisterPipeline(IServiceCollection services)
    {
        services.AddTransient<IIntentParser, RegexIntentParser>();
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        services.AddTransient<IPipelineExecutor, PipelineExecutor>();
        services.AddTransient<ProcessTicketUseCase>();
    }
}
