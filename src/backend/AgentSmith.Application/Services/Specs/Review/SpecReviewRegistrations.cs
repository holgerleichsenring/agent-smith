using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: the spec review's services — the round's evidence check, the attack reviewer and its
/// admission, the round publisher; the cut reviser, its guard and the residue check; 8762g's review loop step. Every review, contract and re-ground service of this series registers
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
            .AddTransient<SpecReviewRoundPublisher>()
            .AddTransient<SpecRevisionGuard>()
            .AddTransient<SpecCutResidues>()
            .AddTransient<ISpecCutReviser, SpecCutReviser>()
            // 2026-10-13-8762g: the loop, its panel, the rewrite and the one mid-run revision publisher; the
            // settings read live per call (the server replaces the loader reader with its store's).
            .AddTransient<ISpecRoundEvidence, SpecRoundEvidence>()
            .AddTransient<SpecReviewPanel>().AddTransient<SpecReviewRevision>()
            .AddTransient<ISpecRederiver, SpecRederiver>().AddTransient<SpecReviewLoop>()
            .AddSingleton<ISpecReviewSettingsReader, LoaderSpecReviewSettingsReader>()
            .AddTransient<ReviewExhaustion>().AddTransient<ReviewObjectionPark>() // 2026-10-13-8762h
            // 2026-10-13-8762i: the design turn's proposal rounds — panel, re-prompt, guard, revision, loop.
            .AddTransient<ProposalAttackPanel>().AddTransient<ProposalCutsReprompt>()
            .AddTransient<ProposalReProposalGuard>().AddTransient<ProposalRoundRevision>().AddTransient<ProposalReviewLoop>()
            .AddTransient<SeriesContractCheck>().AddTransient<SeriesContractReader>() // 2026-10-13-8762j
            .AddTransient<ICommandHandler<ReviewSpecsContext>, Handlers.ReviewSpecsHandler>();
    }
}
