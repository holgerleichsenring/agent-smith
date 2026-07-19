using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0300c: a poll query cancelled by its own timeout tears the SQLite connection
/// down; EF's built-in ConnectionError event logs that as Error (a red FAIL for an
/// expected cancellation). The wiring downgrades EF's own event to Warning — the
/// interceptor still raises Error for genuine failures.
/// </summary>
public sealed class RelationalPersistenceWarningsTests
{
    [Fact]
    public void SqliteCancel_OnPollTimeout_LoggedAsWarningNotError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AgentSmithConfig
        {
            Persistence = new PersistenceConfig
            {
                Provider = "sqlite",
                ConnectionString = "Data Source=:memory:",
            },
        });
        // p0349: the DbContext is bootstrapped from the file (BootstrapConfig).
        services.AddSingleton(new BootstrapConfig(
            new PersistenceConfig { Provider = "sqlite", ConnectionString = "Data Source=:memory:" },
            new Dictionary<string, string>()));
        services.AddRelationalPersistence();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();

        var core = ctx.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();
        core.Should().NotBeNull();
        core!.WarningsConfiguration.GetLevel(RelationalEventId.ConnectionError.Id)
            .Should().Be(LogLevel.Warning);
    }
}
