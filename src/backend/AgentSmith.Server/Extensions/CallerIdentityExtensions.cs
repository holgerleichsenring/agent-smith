using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0503d: role resolution, registered UNCONDITIONALLY — with no authority configured as
/// much as with one. p0503a's route guard enumerates the built route table, and
/// enumeration runs InferMetadata, which throws on a handler parameter type absent from
/// the container; a registration that hung off the authority would fail that guard, which
/// boots without one, with a message about unregistered services that looks nothing like
/// the rule it enforces.
/// </summary>
internal static class CallerIdentityExtensions
{
    internal static IServiceCollection AddCallerIdentity(this IServiceCollection services)
    {
        // The auth block is bootstrap — file plus environment, read before the config
        // store exists. An installation with no block gets the defaults, which resolve
        // no roles and grant nothing.
        services.AddSingleton(sp =>
            sp.GetRequiredService<BootstrapConfigReader>().Read().Auth ?? new TokenAuthorityConfig());
        // The environment read is a captured delegate, not a registered service: the
        // grant is a value this composition root supplies, and a test states what is set
        // instead of mutating the process every other test in the suite shares.
        services.AddSingleton(_ => new AdminGrant(Environment.GetEnvironmentVariable));
        // 2026-08-25-1806: the mapping and its two readers are no longer startup values.
        // The source hands out the mapping in force and rebuilds the readers when a save
        // changes it, so a singleton lifetime here freezes nothing — which is what lets an
        // authorization handler (itself a singleton) keep asking it per request.
        services.AddSingleton<IStoredRoleMapping, StoredRoleMapping>();
        services.AddSingleton<RoleMappingSource>();
        // 2026-08-26-7a51: the resolver notes the caller into an in-memory buffer and
        // returns — the write is somewhere else, so the authorization path never waits on
        // a database and never refuses anybody because a row could not be stored.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CallerObservationBuffer>();
        services.AddSingleton<ICallerObservations>(sp => sp.GetRequiredService<CallerObservationBuffer>());
        services.AddSingleton<AdminRoute>();
        services.AddSingleton<CallerIdentityResolver>();
        // Registered whether or not an authority is, because the anonymous requirements
        // route reads it and that route is mapped unconditionally.
        services.AddSingleton<RefusedToken>();
        services.AddSingleton<RoleMappingMigration>();
        return services;
    }
}
