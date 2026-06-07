using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246b: the opt-in wiring. AddRelationalPersistence swaps the no-op lease for
/// the DB-backed guard ONLY when AGENTSMITH_PERSISTENCE_PROVIDER is set, so the
/// default Server keeps the Redis-only claim path with no DB dependency.
/// </summary>
public sealed class RelationalPersistenceWiringTests
{
    [Fact]
    public void AddRelationalPersistence_WithSqliteEnv_SwapsToDbBackedLease()
    {
        Environment.SetEnvironmentVariable(RelationalPersistenceExtensions.ProviderEnv, "sqlite");
        Environment.SetEnvironmentVariable(RelationalPersistenceExtensions.ConnectionEnv, "Data Source=:memory:");
        try
        {
            using var provider = BuildProvider();
            provider.GetRequiredService<IActiveRunLease>().Should().BeOfType<DbActiveRunLease>();
        }
        finally
        {
            ClearEnv();
        }
    }

    [Fact]
    public void AddRelationalPersistence_WithoutEnv_KeepsNoOpLease()
    {
        ClearEnv();
        using var provider = BuildProvider();
        provider.GetRequiredService<IActiveRunLease>().Should().BeOfType<NoOpActiveRunLease>();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        // Mirror the Server chain: the overrides register the NoOp default first,
        // then AddRelationalPersistence runs and (when configured) RemoveAll-swaps it.
        services.AddSingleton<IActiveRunLease, NoOpActiveRunLease>();
        services.AddRelationalPersistence();
        return services.BuildServiceProvider();
    }

    private static void ClearEnv()
    {
        Environment.SetEnvironmentVariable(RelationalPersistenceExtensions.ProviderEnv, null);
        Environment.SetEnvironmentVariable(RelationalPersistenceExtensions.ConnectionEnv, null);
    }
}
