using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The dashboard's CORS policy. Comma-separated so several dashboard origins (staging +
/// prod, or a reverse-proxy host) can be allowed at once — the env-var was a single
/// origin, which silently broke any dashboard not on :3000. Loopback is always allowed:
/// the dashboard is an operator-local tool and a hostile website cannot forge a loopback
/// origin, so this is safe regardless of environment.
/// </summary>
internal static class DashboardCorsExtensions
{
    private const string OriginEnvVar = "AGENTSMITH_DASHBOARD_ORIGIN";
    private const string DefaultOrigin = "http://localhost:3000";

    internal static IServiceCollection AddDashboardCors(this IServiceCollection services)
    {
        var configured = ConfiguredOrigins();
        return services.AddCors(o => o.AddPolicy(DashboardConstants.CorsPolicy, p => p
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
            .SetIsOriginAllowed(origin => IsAllowed(origin, configured))));
    }

    private static string[] ConfiguredOrigins() =>
        (Environment.GetEnvironmentVariable(OriginEnvVar) ?? DefaultOrigin)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsAllowed(string origin, string[] configured) =>
        configured.Contains(origin, StringComparer.OrdinalIgnoreCase)
        || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.IsLoopback
                || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)));
}
