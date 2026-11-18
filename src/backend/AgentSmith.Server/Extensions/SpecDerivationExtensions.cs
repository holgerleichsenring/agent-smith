using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Derivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>2026-10-26-bdd0e: composition for POST /api/tickets/{project}/{ticketId}/derive-specs and the
/// conversation's derive_specs. Scoped like the init launch it borrows its admission from.</summary>
internal static class SpecDerivationExtensions
{
    internal static IServiceCollection AddSpecDerivation(this IServiceCollection services)
    {
        services.AddProjectInit();
        services.TryAddScoped<DerivationRunRepository>();
        services.TryAddScoped<SpecDerivationGate>();
        services.TryAddScoped<SpecDerivationLauncher>();
        services.TryAddScoped<Services.Deployment.DeployLauncher>(); // 2026-11-18-3863c: queued like a derivation
        services.TryAddScoped<Services.Deployment.DeploymentBindings>();
        return services;
    }
}
