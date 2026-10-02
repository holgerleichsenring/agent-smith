using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-02-075dd: a set's note is one row beside its files — replaced whole, never one of the
/// set's files, kept with a cited set when the conversation is deleted.
/// </summary>
public sealed class ReferenceNoteRepositoryTests
{
    [Fact]
    public async Task ReferenceSetRepository_SetNote_ReplacesAndNeverJoinsTheSetsFiles()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var set = await sets.AddAsync("s-1", [File("app/app.py"), File("app/.env")], CancellationToken.None);
        var notes = new ReferenceNoteRepository(db);

        (await notes.SetAsync("s-1", set.SetId, "first", CancellationToken.None)).Should().BeTrue();
        (await notes.SetAsync("s-1", set.SetId, "Flask app; python3 -m venv", CancellationToken.None)).Should().BeTrue();

        (await notes.NotesAsync("s-1", CancellationToken.None)).Should().Equal(
            new Dictionary<string, string> { [set.SetId] = "Flask app; python3 -m venv" });
        var listed = (await sets.ListAsync("s-1", CancellationToken.None)).Single();
        (listed.Name, listed.Files).Should().Be(("app", 2));
        (await sets.FilesAsync("s-1", set.SetId, CancellationToken.None)).Select(f => f.Path).Should().Equal("app/.env", "app/app.py");
    }

    [Fact]
    public async Task ReferenceSetRepository_SetNote_ForAnotherSessionsSet_IsRefused()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/app.py")], CancellationToken.None);

        (await new ReferenceNoteRepository(db).SetAsync("s-2", set.SetId, "mine now", CancellationToken.None)).Should().BeFalse();
        (await new ReferenceNoteRepository(db).NotesAsync("s-2", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReferenceFileRepository_Delete_KeepsACitedSetsNote()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var cited = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/app.py")], CancellationToken.None);
        var other = await new ReferenceSetRepository(db).AddAsync("s-1", [File("doc/a.md")], CancellationToken.None);
        var notes = new ReferenceNoteRepository(db);
        await notes.SetAsync("s-1", cited.SetId, "recipe", CancellationToken.None);
        await notes.SetAsync("s-1", other.SetId, "gone", CancellationToken.None);

        await new ReferenceFileRepository(db).DeleteBySessionAsync("s-1", new HashSet<string> { cited.SetId }, CancellationToken.None);

        (await notes.NotesAsync("s-1", CancellationToken.None)).Should().Equal(
            new Dictionary<string, string> { [cited.SetId] = "recipe" });
    }

    private static ReferenceFile File(string path) => new() { RelativePath = path, MediaType = "text/plain", Content = [0x61] };
}
