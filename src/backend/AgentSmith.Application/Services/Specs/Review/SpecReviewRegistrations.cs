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
            // 2026-10-13-8762g: the in-run revision publisher; the settings read live per call (the server replaces the
            // loader reader with its store's). 2026-11-19-3e10/4176: no review panel, attack or series reviewer is left.
            .AddTransient<SpecReviewRevision>()
            .AddSingleton<ISpecReviewSettingsReader, LoaderSpecReviewSettingsReader>()
            .AddTransient<SeriesContractCheck>().AddTransient<SeriesContractReader>() // 2026-10-13-8762j
            // 2026-10-13-8762n: the entered spec's citation re-ground — history, regrounder, entry.
            .AddTransient<SpecCitationHistory>().AddTransient<SpecCitationRegrounder>()
            .AddTransient<ISpecEntryGrounding, SpecEntryGrounding>()
            // 2026-11-17-ec48a: the coding master's revise_spec — gate, text, writer and orchestration (3e10: no review).
            .AddTransient<Revision.SpecRevisionGate>()
            .AddTransient<Revision.SpecRevisionText>().AddTransient<Revision.SpecRevisionWriter>()
            .AddTransient<Revision.MasterSpecRevision>();
    }
}
