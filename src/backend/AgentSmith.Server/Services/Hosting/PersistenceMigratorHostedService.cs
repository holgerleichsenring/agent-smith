using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// Applies any pending EF migrations once at startup so the schema exists before
/// the lease / projector touch it. Registered only when relational persistence is
/// configured. EF's migration history table + provider locking make concurrent
/// replicas safe enough for this opt-in path.
/// </summary>
public sealed class PersistenceMigratorHostedService(
    IDbContextFactory<AgentSmithDbContext> contextFactory,
    ILogger<PersistenceMigratorHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
        logger.LogInformation("Applying relational persistence migrations...");
        await ctx.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Relational persistence schema is up to date.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
