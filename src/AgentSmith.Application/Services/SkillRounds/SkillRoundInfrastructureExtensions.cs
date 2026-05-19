using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Skill-round runtime infrastructure: prompt composition, round dispatcher,
/// response parser, buffer dispatcher, blocking-follow-up detection, the
/// discussion / structured round executors, the three round-tool policies
/// (Discussion / Structured / Filter) — added in p0148 to route each round
/// through ISkillRoundToolPolicy — and the per-skill prompt strategies.
/// SkillRoundDispatcher also publishes the tool_set_size concept and is
/// exposed as IConceptWriter for the validate-concepts registry.
/// </summary>
public static class SkillRoundInfrastructureExtensions
{
    public static IServiceCollection AddSkillRoundInfrastructure(this IServiceCollection services)
    {
        services.AddTransient<SourceAnchoringPreamble>();
        services.AddTransient<ObservationBusProjector>();
        services.AddTransient<IPromptComposer, PromptComposer>();
        services.AddTransient<SkillRoundDispatcher>();
        services.AddTransient<ISkillRoundDispatcher>(sp => sp.GetRequiredService<SkillRoundDispatcher>());
        services.AddSingleton<IConceptWriter>(sp => sp.GetRequiredService<SkillRoundDispatcher>());
        services.AddTransient<ISkillResponseParser, SkillResponseParser>();
        services.AddSingleton<ISkillRoundBufferDispatcher, SkillRoundBufferDispatcher>();
        services.AddSingleton<IBlockingFollowUpDetector, BlockingFollowUpDetector>();
        services.AddTransient<IDiscussionRoundExecutor, DiscussionRoundExecutor>();
        services.AddTransient<IStructuredRoundExecutor, StructuredRoundExecutor>();
        services.AddTransient<DiscussionRoundToolPolicy>();
        services.AddTransient<StructuredRoundToolPolicy>();
        services.AddTransient<FilterRoundToolPolicy>();
        services.AddTransient<DefaultSkillPromptStrategy>();
        services.AddTransient<SecuritySkillPromptStrategy>();
        services.AddTransient<ApiSkillPromptStrategy>();
        return services;
    }
}
