using AgentSmith.Infrastructure.Persistence.Configurations;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence;

/// <summary>
/// The relational system-of-record. Doubles as the unit of work: callers stage
/// entity changes and commit with one SaveChangesAsync, which also stamps the
/// EntityBase audit columns. Configuration of the load-bearing tables lives in
/// IEntityTypeConfiguration classes; the uniform Run-child FK length is set here.
/// </summary>
public sealed class AgentSmithDbContext(DbContextOptions<AgentSmithDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<ActiveRun> ActiveRuns => Set<ActiveRun>();
    public DbSet<RunRepo> RunRepos => Set<RunRepo>();
    public DbSet<RunStep> RunSteps => Set<RunStep>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<RunDecision> RunDecisions => Set<RunDecision>();
    public DbSet<RunLlmCall> RunLlmCalls => Set<RunLlmCall>();
    public DbSet<RunArtifact> RunArtifacts => Set<RunArtifact>();
    public DbSet<RunSandbox> RunSandboxes => Set<RunSandbox>();
    public DbSet<TicketLifecycle> TicketLifecycles => Set<TicketLifecycle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new ActiveRunConfiguration());
        modelBuilder.ApplyConfiguration(new TicketLifecycleConfiguration());
        ConfigureRunChildren(modelBuilder);
    }

    // Every Run child carries a string RunId FK referencing Run.Id (a string
    // key) — cap it at the indexed-string length so the FK column matches the PK
    // and the MySQL key-length limit holds. The relationship + cascade delete
    // come from convention (the Run navigation + the Run.* collections).
    private static void ConfigureRunChildren(ModelBuilder modelBuilder)
    {
        Type[] children =
        [
            typeof(RunRepo), typeof(RunStep), typeof(RunEvent), typeof(RunDecision),
            typeof(RunLlmCall), typeof(RunArtifact), typeof(RunSandbox),
        ];
        foreach (var child in children)
            modelBuilder.Entity(child).Property("RunId").HasMaxLength(PersistenceLimits.IndexedString);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    private void StampAudit()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
    }
}
