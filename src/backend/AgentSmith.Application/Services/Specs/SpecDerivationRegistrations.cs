using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0400c: the spec-set feature's own registrations — deriver, parser and its
/// envelope reader, the yaml renderer and set index, reader/writer/publisher over
/// the ticket branch, and the fallback + reporting services. They left the
/// pipeline-handler wall because a registration list that grows without bound is
/// how a 220-line file gets to stay 220 lines.
/// </summary>
public static class SpecDerivationRegistrations
{
    public static IServiceCollection AddSpecDerivation(this IServiceCollection services)
    {
    services.AddTransient<ISpecCutReviewer, SpecCutReviewer>(); // p0422: the design turn's whole-set review and verify_series
    Review.SpecReviewRegistrations.AddSpecReview(services); // 2026-10-13-8762e: the spec review rounds
        services.AddTransient<ForeignCommitProbe>();
        services.AddTransient<HandbackHeadsReader>();
    // 2026-10-06-03c7d: a series is one manifest under series/ and its specs in specs/planned/.
    services.AddTransient<ISpecSetReader, SeriesReader>();
    // 2026-10-12-f5f7b: every run repository's sub-series, joined into one set.
    services.AddTransient<SubSeriesReader>().AddBranchSeriesRead(); // 2026-11-17-a0b6b: the reading shared with the ticket readers
    services.AddTransient<ISpecSetWriter, SeriesWriter>();
    services.AddTransient<SeriesMemberCommit>().AddTransient<SeriesWriteRollback>(); // 2026-10-12-f5f7f
    // 2026-10-12-f5f7d: each spec placed in the repository it names; read, ids, filing per member.
    services.AddTransient<SeriesRepos>().AddTransient<SeriesTakenIds>().AddTransient<SeriesBranchRead>();
    services.AddTransient<FiledSeriesWrite>();
    services.AddTransient<SeriesStaleFiles>();
    services.AddTransient<ISpecSetPublisher, SpecSetPublisher>().AddTransient<SpecSetAnnouncer>();
    services.AddTransient<SpecSetContextPublication>().AddTransient<SpecSetUnchanged>(); // 2026-11-17-7c9ba
    services.AddTransient<SpecSetPointerRecorder>(); // 2026-09-08-4aa9: the marker's commit moves the pointer too
    services.AddTransient<ISpecPullRequestOpener, SpecPullRequestOpener>();
    services.AddTransient<DerivedSpecYamlRenderer>();
    // 2026-09-22-b6ad: the files a series holds, for the run's publish and for the checkout-free
    // write filing makes onto the ticket branch.
    services.AddTransient<SeriesFiles>();
    services.AddTransient<FiledSpecBranch>();
    services.AddTransient<SpecReposRule>(); // 2026-10-12-3a36e
    // 2026-09-07-b7e2: the resolver that decides a fact in code.
    services.AddTransient<FactResolver>();
    // 2026-09-13-9f84: what an opened template declares as its own proof, read and reported.
    services.AddTransient<TemplateProofReport>();
    services.AddTransient<TemplateProofRead>();
    services.AddTransient<ProjectTemplateScopes>().AddTransient<RunTemplateScopes>();
    // 2026-09-13-6f35: the coding master opens the same declarations the derivation cites.
    services.AddTransient<Handlers.MasterTemplateScopes>().AddTransient<Handlers.MasterTemplateAdvice>(); // + 54bea
    services.AddTransient<DerivationLookFactory>();
    services.TryAddSingleton<IPackageEcosystemDetector, Sandbox.PackageEcosystemDetector>();
    services.AddTransient<SpecSourceResolver>().AddTransient<ApprovedContinuation>().AddTransient<ContinuationMerge>(); // 9a59l
    // 2026-10-06-03c7c: a series' base id is minted in code, once, and read back thereafter.
    services.TryAddSingleton(TimeProvider.System);
    services.AddTransient<SeriesIdFactory>();
    services.AddTransient<SeriesResolver>();
    services.AddTransient<SpecStandIns>(); // 2026-11-19-6b0ba
    services.AddTransient<SpecRefusalReporter>();
    services.AddTransient<SpecSetTicketCommenter>();
    services.AddTransient<AgentSmith.Contracts.Reviews.IFullSetPrNotice, Rework.FullSetPrNotice>();
    services.AddTransient<AgentSmith.Contracts.Reviews.IHandbackPrNotice, Rework.HandbackPrNotice>();
    services.AddTransient<UnansweredQuestionPin>();
    services.AddTransient<UnansweredQuestionNotice>();
    services.AddTransient<SpecHandbackRepeat>(); // 2026-09-07-bd7a: the repeat guard reads the thread
    services.AddTransient<Handlers.SpecHandbackPark>(); // 2026-09-22-7c41b: reports what the tracker did
    services.AddTransient<Handlers.DerivationQuestion>(); // 2026-10-26-bdd0c
    services.AddTransient<Handlers.InlineHandbackAsk>(); // 2026-11-02-2b26d
    services.AddTransient<Handlers.InlineDerivationAsk>(); // 2026-11-04-9e8c
    services.AddTransient<SpecParkStatusResolver>();
    services.AddTransient<ISpecProgressRecorder, SpecProgressRecorder>(); // p0466
    services.TryAddSingleton<ISpecSetPointerStore, Persistence.InMemorySpecSetPointerStore>();
    // 2026-09-17-0e79a: what a person approved in the design conversation — its store and the one
    // resolver both ScopeRepos and DeriveSpec ask. 2026-09-22-6ad7: the record no longer competes
    // with the branch for the SET; it hands one over only when the branch carries nothing.
    services.TryAddSingleton<ISpecApprovalStore, Persistence.InMemorySpecApprovalStore>();
    // 2026-09-25-3c7aa: asked where an incoming envelope is built, on both routed paths.
    services.TryAddSingleton<ApprovedRecordProbe>();
    // 2026-09-25-c1f7: the other reader of the record — what the poll's query has to ADMIT. It is
    // registered beside the store rather than beside the query builder, because the builder takes
    // it optionally and a process with no store must keep resolving the builder.
    services.TryAddSingleton<Polling.ApprovedTicketAdmission>();
    services.TryAddSingleton<Polling.PolledTicketEnvelope>();
    services.AddTransient<ApprovedSpecSetResolver>();
    services.AddTransient<ApprovedSpecSetCarrier>();
    services.AddTransient<ApprovedSetHandoff>();
    services.AddTransient<FiledTicketSpecGate>();
    // 2026-09-17-0e79b: an approved set is never re-cut — the input is reported instead.
    services.AddTransient<ApprovedSetKeptNotice>();
    // 2026-09-17-042eh: where a spec began, which its diff and the shortfall read.
    services.AddTransient<SpecStartHeads>();
        return services;
    }
}
