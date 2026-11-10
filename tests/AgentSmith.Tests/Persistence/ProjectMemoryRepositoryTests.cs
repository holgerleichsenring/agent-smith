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

    [Fact]
    public async Task ProjectMemoryRepository_ConcurrentUpsertSameSlug_OneRowNewestWins()
    {
        var id = AddProject("sample");
        await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Repo.UpsertAsync(id, Record("x", $"v{i}"), default)));
        await Repo.UpsertAsync(id, Record("x", "last"), default);

        (await Repo.ListAsync(id, default)).Should().ContainSingle().Which.Description.Should().Be("last");
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
