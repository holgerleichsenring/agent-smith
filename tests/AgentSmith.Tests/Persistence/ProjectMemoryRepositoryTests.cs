using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-10-fcd7a: a project's memories over the shipped migrations — one row per slug, bounded text, the
/// ledger rows going with a deleted entry, the cascade from the project's config entity, and a forced config
/// import that keeps the Ids (and so the memories) of every project the file still holds.
/// </summary>
public sealed class ProjectMemoryRepositoryTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    public ProjectMemoryRepositoryTests()
    {
        using var db = _store.Context();
        db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
    }

    private ProjectMemoryRepository Repo => new(_store.ScopeFactory, new SqliteUniqueViolationTranslator());

    private static ProjectMemoryRecord Record(string slug, string description = "d", string body = "b") =>
        new(slug, "feedback", description, body, ProjectMemorySourceKinds.Conversation, "conv-7", "3", "Holger");

    private static ConfigDocWrite Project(string id) =>
        new(UnmovedTicketRepository.ProjectDocType, id, "{}", null, [], "importer");

    private long AddProject(string name)
    {
        using var db = _store.Context();
        var row = new ConfigEntity { EntityType = UnmovedTicketRepository.ProjectDocType, EntityId = name, Doc = "{}", Version = 1 };
        db.ConfigEntities.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    [Fact]
    public async Task ProjectMemoryRepository_UpsertBySlug_ReplacesAndKeepsSourceKindIdRefAuthor()
    {
        var id = AddProject("sample");
        await Repo.UpsertAsync(id, Record("no-shims"), default);
        await Repo.UpsertAsync(id, Record("no-shims", "second"), default);

        var stored = (await Repo.ListAsync(id, default)).Should().ContainSingle().Subject;
        stored.Description.Should().Be("second");
        (stored.SourceKind, stored.SourceId, stored.SourceRef, stored.Author)
            .Should().Be((ProjectMemorySourceKinds.Conversation, "conv-7", "3", "Holger"));
    }

    /// <summary>2026-11-11-205fg: two writers on separate connections both read "no row" before either saves; the second
    /// save hits the unique index and takes the retry, so one row stands with the newest value.</summary>
    [Fact]
    public async Task ProjectMemoryRepository_TwoContextsBothSeeNoRow_OneRowNewestWinsRetryTaken()
    {
        ServerStateStore? disk = null;
        var armed = true;
        disk = new ServerStateStore(onDisk: true, unitOfWork: db =>
        {
            db.SavingChanges += (_, _) =>
            {
                if (!armed) return;
                armed = false;
                // The other writer: it read no row too, and saves first on a connection of its own.
                using var other = disk!.Context();
                other.Set<ProjectMemory>().Add(new ProjectMemory
                {
                    ProjectId = ProjectOf(other), Slug = "x", Type = "feedback", Description = "other", Body = "b",
                    SourceKind = ProjectMemorySourceKinds.Run, SourceId = "run-1",
                });
                other.SaveChanges();
            };
            return db;
        });
        using (disk)
        {
            long id;
            using (var db = disk.Context())
            {
                db.ConfigEntities.Add(new ConfigEntity { EntityType = UnmovedTicketRepository.ProjectDocType, EntityId = "sample", Doc = "{}", Version = 1 });
                armed = false;
                db.SaveChanges();
                id = db.ConfigEntities.Single().Id;
            }
            armed = true;
            var unique = new CountingUnique();

            await new ProjectMemoryRepository(disk.ScopeFactory, unique).UpsertAsync(id, Record("x", "newest"), default);

            unique.Violations.Should().Be(1, "the second save hit the unique index and the retry ran");
            (await new ProjectMemoryRepository(disk.ScopeFactory, unique).ListAsync(id, default))
                .Should().ContainSingle().Which.Description.Should().Be("newest");
        }
    }

    private static long ProjectOf(AgentSmith.Infrastructure.Persistence.AgentSmithDbContext db) => db.ConfigEntities.Single().Id;

    private sealed class CountingUnique : AgentSmith.Infrastructure.Persistence.Contracts.IUniqueViolationTranslator
    {
        public int Violations { get; private set; }

        public bool IsUniqueViolation(Exception exception)
        {
            var hit = new SqliteUniqueViolationTranslator().IsUniqueViolation(exception);
            if (hit) Violations++;
            return hit;
        }
    }

    [Fact]
    public async Task ProjectMemoryRepository_BodyOrDescriptionOverCap_IsRefused()
    {
        var id = AddProject("sample");
        var longDescription = () => Repo.UpsertAsync(id, Record("a", new string('d', ProjectMemoryLimits.Description + 1)), default);
        var longBody = () => Repo.UpsertAsync(id, Record("b", body: new string('b', ProjectMemoryLimits.Body + 1)), default);

        await longDescription.Should().ThrowAsync<ArgumentException>().WithMessage("*description*");
        await longBody.Should().ThrowAsync<ArgumentException>().WithMessage("*body*");
        (await Repo.ListAsync(id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task ProjectMemoryRepository_ResolveProject_MissingRowAnswersAsEmptyStore()
    {
        var id = AddProject("sample");

        (await Repo.ResolveProjectAsync("sample", default)).Should().Be(id);
        (await Repo.ResolveProjectAsync("unknown", default)).Should().BeNull();
    }

    [Fact]
    public async Task ProjectMemoryRepository_Delete_RemovesLedgerRows()
    {
        var id = AddProject("sample");
        await Repo.UpsertAsync(id, Record("x"), default);
        var ledger = new MemoryEvidenceRepository(_store.ScopeFactory, new());
        var fact = new MemoryEvidenceFact(ProjectMemorySource.LedgerKey(id), "x", MemoryEvidenceKinds.Relied, 0,
            "sample", "A-1", "run-1", "s-a", null, DateTimeOffset.UtcNow);
        await ledger.AppendAsync(fact, default);
        await ledger.AppendAsync(fact with { Slug = "y" }, default);

        (await Repo.DeleteAsync(id, "x", default)).Should().BeTrue();

        (await Repo.ListAsync(id, default)).Should().BeEmpty();
        (await ledger.OfRepositoryAsync(ProjectMemorySource.LedgerKey(id), default)).Should().ContainSingle()
            .Which.Slug.Should().Be("y");
    }

    [Fact]
    public async Task ProjectMemory_ConfigEntityDeleted_Cascades()
    {
        var id = AddProject("sample");
        await Repo.UpsertAsync(id, Record("x"), default);

        await using (var db = _store.Context())
        {
            db.ConfigEntities.Remove(await db.ConfigEntities.SingleAsync(e => e.Id == id));
            await db.SaveChangesAsync();
        }

        await using var read = _store.Context();
        (await read.Set<ProjectMemory>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConfigImport_Force_KeepsIdsOfSurvivingEntities_AndProjectMemories()
    {
        var id = AddProject("sample");
        await Repo.UpsertAsync(id, Record("x"), default);

        await using (var db = _store.Context()) new ConfigImportRepository(db).Import([Project("sample")], force: true);

        (await Repo.ResolveProjectAsync("sample", default)).Should().Be(id);
        (await Repo.ListAsync(id, default)).Should().ContainSingle();
    }

    [Fact]
    public async Task ConfigImport_Force_DeletesEntitiesMissingFromTheFile()
    {
        AddProject("gone");
        AddProject("kept");

        await using (var db = _store.Context()) new ConfigImportRepository(db).Import([Project("kept")], force: true);

        await using var read = _store.Context();
        (await read.ConfigEntities.Select(e => e.EntityId).ToListAsync()).Should().Equal("kept");
    }

    [Fact]
    public async Task ConfigImport_Force_ClearsRefsFirst()
    {
        AddProject("kept");
        AddProject("gone");
        await using (var db = _store.Context())
        {
            db.ConfigRefs.Add(new ConfigRef
            {
                FromType = UnmovedTicketRepository.ProjectDocType, FromId = "kept",
                ToType = UnmovedTicketRepository.ProjectDocType, ToId = "gone",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _store.Context()) new ConfigImportRepository(db).Import([Project("kept")], force: true);

        await using var read = _store.Context();
        (await read.ConfigRefs.CountAsync()).Should().Be(0);
        (await read.ConfigEntities.CountAsync()).Should().Be(1);
    }

    public void Dispose() => _store.Dispose();
}
