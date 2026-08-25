using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-25-8c97: which build this server is, and the comparison against the build a
/// caller says it is. Registered with the rest of the diagnostic surface and needing
/// nothing but the environment, so the answer survives every dependency it reports on.
/// </summary>
internal static class BuildIdentityExtensions
{
    internal static IServiceCollection AddBuildIdentity(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new BuildIdentity(
            Environment.GetEnvironmentVariable(BuildIdentity.RevisionVariable),
            Environment.GetEnvironmentVariable(BuildIdentity.VersionVariable)));
        services.AddSingleton<IBuildMismatchDetector, BuildMismatchDetector>();
        return services;
    }
}
