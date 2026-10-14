using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: the spec review's services — the round's evidence check, the attack reviewer and its
/// admission, the round publisher. Every review, contract and re-ground service of this series registers
/// here, so <see cref="SpecDerivationRegistrations"/> holds one call for all of them.
/// </summary>
public static class SpecReviewRegistrations
{
    public static IServiceCollection AddSpecReview(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddTransient<SpecRoundEvidence>()
            .AddTransient<SpecAttackAdmission>()
            .AddTransient<ISpecAttackReviewer, SpecAttackReviewer>()
            .AddTransient<SpecReviewRoundPublisher>();
    }
}
