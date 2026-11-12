using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-11-12-cd4ck: the conversation's notes are one row beside its uploads — replaced whole, unbounded, never one
/// of a set's files; legacy per-upload notes are read until the first write; a cited set keeps them on delete.
/// </summary>
public sealed class ConversationNotesRepositoryTests
{
    [Fact]
    public async Task ConversationNotesRepository_WriteThenRead_ReturnsTheWholeFile()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [File("app/app.py"), File("app/.env")], CancellationToken.None);
        var notes = new ConversationNotesRepository(db);
        var big = new string('n', 50_000);

        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().BeNull();
        await notes.WriteAsync("s-1", "first", CancellationToken.None);
        await notes.WriteAsync("s-1", big, CancellationToken.None);

        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().Be(big);
        (await notes.ReadAsync("s-2", CancellationToken.None)).Should().BeNull();
        (await sets.ListAsync("s-1", CancellationToken.None)).Single().Files.Should().Be(2);
        (await sets.FilesAsync("s-1", set.SetId, CancellationToken.None)).Select(f => f.Path).Should().Equal("app/.env", "app/app.py");
    }

    [Fact]
    public async Task ConversationNotesRepository_LegacySetNotes_AreReadUntilTheFirstWrite()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        db.Add(new ReferenceFile
        {
            SessionId = "s-1", SetId = "set-a", Kind = ReferenceFileKind.Note, RelativePath = "NOTE.md",
            MediaType = "text/markdown", Content = "Flask app"u8.ToArray(),
        });
        await db.SaveChangesAsync();
        var notes = new ConversationNotesRepository(db);

        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().Be("From the note on upload set set-a:\nFlask app");
        await notes.WriteAsync("s-1", "merged", CancellationToken.None);
        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().Be("merged");
    }

    [Fact]
    public async Task ReferenceFileRepository_Delete_KeepsTheNotesWithACitedSet()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var cited = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/app.py")], CancellationToken.None);
        var notes = new ConversationNotesRepository(db);
        await notes.WriteAsync("s-1", "recipe", CancellationToken.None);

        await new ReferenceFileRepository(db).DeleteBySessionAsync("s-1", new HashSet<string> { cited.SetId }, CancellationToken.None);
        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().Be("recipe");

        await new ReferenceFileRepository(db).DeleteBySessionAsync("s-1", new HashSet<string>(), CancellationToken.None);
        (await notes.ReadAsync("s-1", CancellationToken.None)).Should().BeNull();
    }

    private static ReferenceFile File(string path) => new() { RelativePath = path, MediaType = "text/plain", Content = [0x61] };
}
