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
            // 2026-10-13-8762g: the panel and the rewrite; the settings read live per call (the server replaces the
            // loader reader with its store's). 2026-11-18-9c1cb: the run-time review loop is gone.
            .AddTransient<ISpecRoundEvidence, SpecRoundEvidence>()
            .AddTransient<SpecReviewPanel>().AddTransient<SpecReviewRevision>()
            .AddSingleton<ISpecReviewSettingsReader, LoaderSpecReviewSettingsReader>()
            .AddTransient<SeriesContractCheck>().AddTransient<SeriesContractReader>() // 2026-10-13-8762j
            // 2026-10-13-8762k: the set-scope round — reviewer, admission, and the run's round.
            .AddTransient<SeriesContractAdmission>().AddTransient<ISeriesContractReviewer, SeriesContractReviewer>()
            // 2026-10-13-8762n: the entered spec's citation re-ground — history, regrounder, entry.
            .AddTransient<SpecCitationHistory>().AddTransient<SpecCitationRegrounder>()
            .AddTransient<ISpecEntryGrounding, SpecEntryGrounding>()
            // 2026-11-17-ec48a: the coding master's revise_spec — its one review, gate, text, writer and orchestration.
            .AddTransient<SpecRevisionReview>().AddTransient<Revision.SpecRevisionGate>()
            .AddTransient<Revision.SpecRevisionText>().AddTransient<Revision.SpecRevisionWriter>()
            .AddTransient<Revision.MasterSpecRevision>();
    }
}
