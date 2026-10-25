using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324c: the memory ledger's application side — the DB-free store a persistence composition
/// replaces, the repository key, the citation check, the run-end outcome, and
/// 2026-10-25-9324d's standing, the master's index and the MEMORY.md labels.
/// </summary>
public static class MemoryStandingRegistrations
{
    public static IServiceCollection AddMemoryStanding(this IServiceCollection services)
    {
        services.TryAddSingleton<IMemoryEvidenceStore, NullMemoryEvidenceStore>(); // 2026-10-25-9324a
        services.TryAddSingleton<MemoryRepositoryKey>();
        services.TryAddSingleton<MemoryCitationResolver>(); // 2026-10-25-9324b
        services.TryAddTransient<MemoryOutcomeRecorder>();
        services.TryAddSingleton<MemoryStandingCalculator>(); // 2026-10-25-9324d
        services.TryAddSingleton<MemoryIndexComposer>();
        services.TryAddSingleton<MemoryIndexLabels>();
        services.TryAddTransient<MemoryStandingReader>();
        services.TryAddTransient<MemoryIndexLabelWriter>();
        return services;
    }
}
