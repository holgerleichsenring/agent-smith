using AgentSmith.Application.Services.Specs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The two services that judge a phase against something outside the run: the delivery
/// account, which reads the branch and asks whether the work satisfies the contract, and
/// the spec review, which reads the repository and asks whether the contract can be
/// satisfied at all. They register together because they are one mechanism pointed at
/// opposite ends of a run.
/// </summary>
public static class SpecJudgementServicesExtensions
{
    public static IServiceCollection AddSpecJudgementServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // p0420: delivery is accounted for against the branch, not inferred from the run.
        services.AddTransient<DeliveryDiff>();
        services.AddTransient<SpecAccountCall>().AddTransient<AccountCalls>();
        services.AddTransient<ISpecAccountant, SpecAccountant>();
        services.AddTransient<SpecReviewCall>().AddTransient<SpecReviewPass>();
        services.AddTransient<ISpecReviewer, SpecReviewer>();
        return services;
    }
}
