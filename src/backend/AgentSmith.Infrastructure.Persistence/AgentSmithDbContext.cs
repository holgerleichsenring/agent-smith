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
    // p0466: one row per derived phase — the phase as an addressable thing.
    public DbSet<RunPhase> RunPhases => Set<RunPhase>();
    public DbSet<RunLlmCall> RunLlmCalls => Set<RunLlmCall>();
    public DbSet<RunArtifact> RunArtifacts => Set<RunArtifact>();
    public DbSet<RunSandbox> RunSandboxes => Set<RunSandbox>();
    public DbSet<SpecDialogSession> SpecDialogSessions => Set<SpecDialogSession>();
    public DbSet<QueuedTicket> QueuedTickets => Set<QueuedTicket>();

    // p0393a: pointer at the spec set that lives in git on the ticket branch.
    public DbSet<TicketSpecSet> TicketSpecSets => Set<TicketSpecSet>();
    // p0327: durable dialogue — parked runs + the answer inbox.
    public DbSet<RunCheckpoint> RunCheckpoints => Set<RunCheckpoint>();
    public DbSet<DialogueAnswerEntry> DialogueAnswers => Set<DialogueAnswerEntry>();
    // p0328: the ratified expectation per run (the acceptance contract).
    public DbSet<RunExpectation> RunExpectations => Set<RunExpectation>();
    public DbSet<RunCriterionJudgement> RunCriterionJudgements => Set<RunCriterionJudgement>(); // e257
    // p0336: the per-run capacity footprint + reservation ledger.
    public DbSet<RunCapacity> RunCapacities => Set<RunCapacity>();
    // p0349: config as a DB entity-document store — the doc rows, the single audit
    // history, and the reference-graph edges.
    public DbSet<ConfigEntity> ConfigEntities => Set<ConfigEntity>();
    public DbSet<ConfigEntityVersion> ConfigEntityVersions => Set<ConfigEntityVersion>();
    public DbSet<ConfigRef> ConfigRefs => Set<ConfigRef>();
    // 2026-08-26-7a51: the callers this installation has seen, so a role is granted to a
    // person picked from a list rather than to an identifier typed from a console.
    public DbSet<ObservedCallerEntity> ObservedCallers => Set<ObservedCallerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new ActiveRunConfiguration());
        modelBuilder.ApplyConfiguration(new SpecDialogSessionConfiguration());
        modelBuilder.ApplyConfiguration(new QueuedTicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketSpecSetConfiguration()); // p0390
        modelBuilder.ApplyConfiguration(new RunCheckpointConfiguration());
        modelBuilder.ApplyConfiguration(new DialogueAnswerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new RunExpectationConfiguration()); // p0328
        modelBuilder.ApplyConfiguration(new RunCriterionJudgementConfiguration()); // 2026-08-25-e257
        modelBuilder.ApplyConfiguration(new RunCapacityConfiguration()); // p0336
        modelBuilder.ApplyConfiguration(new ConfigEntityConfiguration()); // p0349
        modelBuilder.ApplyConfiguration(new ConfigEntityVersionConfiguration()); // p0349
        modelBuilder.ApplyConfiguration(new ConfigRefConfiguration()); // p0349
        modelBuilder.ApplyConfiguration(new RunPhaseConfiguration()); // p0466
        modelBuilder.ApplyConfiguration(new ObservedCallerConfiguration()); // 2026-08-26-7a51
        new RunChildConfiguration().Apply(modelBuilder);
        // p0388a: applied AFTER the child loop so the per-step trail index is
        // added alongside — not instead of — the uniform RunId index.
        modelBuilder.ApplyConfiguration(new RunEventConfiguration());
        new RunRecordIdentityConfiguration(Database.ProviderName).Apply(modelBuilder); // 2026-08-25-61f1
        new MoneyPrecisionConfiguration(Database.ProviderName).Apply(modelBuilder); // 2026-08-28-b883
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
