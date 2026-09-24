using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0490: the init-project pipeline's own tail — commit the generated files and open a
/// pull request per repo, cross-link the siblings, then finish what was opened when the
/// launch carried the operator's auto-accept. Registered together because they are one
/// story about one pipeline, and because the general handler registry is at its size
/// limit and may only get shorter.
/// </summary>
public static class InitProjectHandlersExtensions
{
    public static IServiceCollection AddInitProjectHandlers(this IServiceCollection services)
    {
        // 2026-09-23-4711: one component's producer match, extracted from the dispatcher.
        services.AddTransient<BootstrapRoundMatch>();
        // 2026-09-23-4711: the retirement runs between the rounds and the commit — a context
        // the derivation dropped is moved aside before the pull request is composed.
        services.AddTransient<BootstrapContextRetirement>();
        services.AddTransient<ICommandHandler<BootstrapRetireContext>, BootstrapRetireHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<PrCrossLinkContext>, PrCrossLinkHandler>();
        services.AddTransient<ICommandHandler<InitCompleteContext>, InitCompleteHandler>();
        return services;
    }
}
