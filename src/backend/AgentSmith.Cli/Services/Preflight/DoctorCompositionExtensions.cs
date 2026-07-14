using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Services.Preflight;

/// <summary>
/// p0324: doctor-only additions on top of the one-shot CLI graph — the shared
/// preflight core plus the CLI-side probe seams (real sandbox round-trip through the
/// composed ISandboxFactory; env-scoped Redis, server-owned DB skipped with reason).
/// </summary>
internal static class DoctorCompositionExtensions
{
    public static IServiceCollection AddDoctorPreflight(this IServiceCollection services)
    {
        services.AddPreflight();
        services.AddSingleton<IPreflightSandboxProbe, SandboxRoundTripProbe>();
        services.AddSingleton<IPreflightInfraProbe, CliInfraPreflightProbe>();
        services.AddSingleton<DoctorExecutor>();
        return services;
    }
}
