using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Security;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<RoleCatalog>();
        services.AddSingleton<CallerRoleReader>();
        services.AddSingleton<CallerIdentityResolver>();
        return services;
    }
}
