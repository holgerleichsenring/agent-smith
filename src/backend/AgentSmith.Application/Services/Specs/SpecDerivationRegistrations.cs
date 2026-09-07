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
    services.AddTransient<ISpecCutReviewer, SpecCutReviewer>(); // p0422: the cut is reviewed before it is built
        services.AddTransient<ISpecSetDeriver, SpecSetDeriver>();
    services.AddTransient<ISpecSetReader, SpecSetReader>();
    services.AddTransient<ISpecSetWriter, SpecSetWriter>();
    services.AddTransient<ISpecSetPublisher, SpecSetPublisher>();
    services.AddTransient<ISpecPullRequestOpener, SpecPullRequestOpener>();
    services.AddTransient<DerivedPhaseYamlRenderer>();
    services.AddTransient<SpecSetIndex>();
    services.AddTransient<SpecDerivationEnvelope>();
    services.AddTransient<SpecDerivationParser>();
    // 2026-09-07-b7e2: the derivation may look before it writes — its call, its tool
    // host over the run's sandboxes, and the resolver that decides a fact in code.
    services.AddTransient<DerivedPhaseBuilder>();
    services.AddTransient<FactResolver>();
    services.AddTransient<SpecDerivationCall>();
    services.AddTransient<DerivationLookFactory>();
    services.TryAddSingleton<IPackageEcosystemDetector, Sandbox.PackageEcosystemDetector>();
    services.AddTransient<SpecSourceResolver>();
    services.AddTransient<SpecFallback>();
    services.AddTransient<SpecCutGate>();
    services.AddTransient<SpecRefusalReporter>();
    services.AddTransient<SpecSetTicketCommenter>();
    services.AddTransient<UnansweredQuestionPin>();
    services.AddTransient<UnansweredQuestionNotice>();
    services.AddTransient<SpecParkStatusResolver>();
    services.AddTransient<IPhaseProgressRecorder, PhaseProgressRecorder>(); // p0466
    services.TryAddSingleton<ISpecSetPointerStore, Persistence.InMemorySpecSetPointerStore>();
        return services;
    }
}
