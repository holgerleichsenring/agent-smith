using AgentSmith.Infrastructure.Persistence.Configurations;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    public DbSet<SpecDialogSession> SpecDialogSessions => Set<SpecDialogSession>();
    public DbSet<QueuedTicket> QueuedTickets => Set<QueuedTicket>();
    // p0327: durable dialogue — parked runs + the answer inbox.
    public DbSet<RunCheckpoint> RunCheckpoints => Set<RunCheckpoint>();
    public DbSet<DialogueAnswerEntry> DialogueAnswers => Set<DialogueAnswerEntry>();
    // p0328: the ratified expectation per run (the acceptance contract).
    public DbSet<RunExpectation> RunExpectations => Set<RunExpectation>();
    // p0336: the per-run capacity footprint + reservation ledger.
    public DbSet<RunCapacity> RunCapacities => Set<RunCapacity>();
    // p0349: config as a DB entity-document store — the doc rows, the single audit
    // history, and the reference-graph edges.
    public DbSet<ConfigEntity> ConfigEntities => Set<ConfigEntity>();
    public DbSet<ConfigEntityVersion> ConfigEntityVersions => Set<ConfigEntityVersion>();
    public DbSet<ConfigRef> ConfigRefs => Set<ConfigRef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new ActiveRunConfiguration());
        modelBuilder.ApplyConfiguration(new SpecDialogSessionConfiguration());
        modelBuilder.ApplyConfiguration(new QueuedTicketConfiguration());
        modelBuilder.ApplyConfiguration(new RunCheckpointConfiguration());
        modelBuilder.ApplyConfiguration(new DialogueAnswerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new RunExpectationConfiguration()); // p0328
        modelBuilder.ApplyConfiguration(new RunCapacityConfiguration()); // p0336
        modelBuilder.ApplyConfiguration(new ConfigEntityConfiguration()); // p0349
        modelBuilder.ApplyConfiguration(new ConfigEntityVersionConfiguration()); // p0349
        modelBuilder.ApplyConfiguration(new ConfigRefConfiguration()); // p0349
        ConfigureRunChildren(modelBuilder);
    }

    // Run children carry a plain indexed RunId — NOT an enforced FK. A child
    // (an artifact, a trail event) can be written before/without its Run row
    // (projection ordering, the container path), and an enforced FK would LOSE
    // that data on a constraint failure. So the Run.* collections are unmapped
    // in-memory holders (populated by DbRunStore via RunId queries), and each
    // child gets a length-capped, indexed RunId column instead of a relationship.
    private static void ConfigureRunChildren(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Run>().Ignore(r => r.Repos).Ignore(r => r.Steps).Ignore(r => r.Events)
            .Ignore(r => r.Decisions).Ignore(r => r.LlmCalls).Ignore(r => r.Artifacts).Ignore(r => r.Sandboxes);

        Type[] children =
        [
            typeof(RunRepo), typeof(RunStep), typeof(RunEvent), typeof(RunDecision),
            typeof(RunLlmCall), typeof(RunArtifact), typeof(RunSandbox),
        ];
        foreach (var child in children)
        {
            var entity = modelBuilder.Entity(child);
            entity.Property("RunId").HasMaxLength(PersistenceLimits.IndexedString);
            entity.HasIndex("RunId");
        }
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

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

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
