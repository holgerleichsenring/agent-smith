using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324c: the memory ledger's application side — the DB-free store a persistence composition
/// replaces, the repository key, the citation check and the run-end outcome.
/// </summary>
public static class MemoryStandingRegistrations
{
    public static IServiceCollection AddMemoryStanding(this IServiceCollection services)
    {
        services.TryAddSingleton<IMemoryEvidenceStore, NullMemoryEvidenceStore>(); // 2026-10-25-9324a
        services.TryAddSingleton<MemoryRepositoryKey>();
        services.TryAddSingleton<MemoryCitationResolver>(); // 2026-10-25-9324b
        services.TryAddTransient<MemoryOutcomeRecorder>();
        return services;
    }
}
