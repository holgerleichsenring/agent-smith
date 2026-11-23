using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>2026-11-23-933db: the database answers to the PoC questions a run asks — whether its project is a PoC
/// project, and the rows and bytes its intake reads and writes. They replace the no-database defaults.</summary>
public static class PocStoreExtensions
{
    public static IServiceCollection AddPocStores(this IServiceCollection services) =>
        services.AddSingleton<IPocProjects, DbPocProjects>().AddSingleton<IPocIntakeStore, DbPocIntakeStore>()
            .AddSingleton<IPocScanStore, DbPocScanStore>(); // 2026-11-23-933dc
}
