using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

public static partial class ServiceCollectionExtensions
{
    // p0111c phase-based triage machinery.
    // p0143: TriageRationaleParser + TriageOutputValidator retired (selector is by-construction valid).
    // p0125b: activation expression pipeline (tokenizer/parser/evaluator are stateless,
    // so singleton is safe; no production runtime path consumes them yet).
    // p0127b: triage pre-filter + post-LLM specificity tie-break.
    private static void AddTriageServices(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<TriageContext>, TriageHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<DeterministicTriageSelector>();
        services.AddTransient<TriageLabelOverrideApplier>();
        services.AddTransient<ProjectMapExcerptBuilder>();
        services.AddTransient<PhaseCommandExpander>();
        services.AddSingleton<SinglePhaseCollapser>();          // p0131c-pre
        services.AddTransient<ITriageOutputProducer, TriageOutputProducer>();
        services.AddTransient<StructuredTriageStrategy>();
        services.AddTransient<ITriageStrategySelector, TriageStrategySelector>();
        services.AddTransient<ICommandHandler<PhaseAdvanceContext>, PhaseAdvanceHandler>();
        services.AddTransient<ICommandHandler<LoadSkillsContext>, LoadSkillsHandler>();
        services.AddSingleton<ActivationExpressionTokenizer>();
        services.AddSingleton<ActivationExpressionParser>();
        services.AddSingleton<ActivationEvaluator>();
        services.AddSingleton<ActivationSkillFilter>();
        services.AddSingleton<ActivationSpecificityScorer>();
        services.AddSingleton<PhaseSpecificityTrimmer>();
    }
}
