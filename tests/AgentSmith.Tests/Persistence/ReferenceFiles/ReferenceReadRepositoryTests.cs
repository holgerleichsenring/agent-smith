using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-11-04-96a0c: what earlier turns opened of a set — merged on every write, read as the union of every row, gone
/// with the set.
/// </summary>
public sealed class ReferenceReadRepositoryTests
{
    [Fact]
    public async Task ReferenceReadRepository_AddAsync_MergesWithTheStoredPaths()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/a.cs"), File("app/b.cs")], default);
        var reads = new ReferenceReadRepository(db);

        await reads.AddAsync("s-1", set.SetId, ["app/a.cs"], default);
        var merged = await reads.AddAsync("s-1", set.SetId, ["app/b.cs", "app/a.cs"], default);

        merged.Should().BeEquivalentTo(["app/a.cs", "app/b.cs"]);
        (await reads.ReadsAsync("s-1", default))[set.SetId].Should().BeEquivalentTo(["app/a.cs", "app/b.cs"]);
        (await db.Set<ReferenceFile>().CountAsync(f => f.Kind == ReferenceFileKind.Reads)).Should().Be(1,
            "a write deletes the rows it merged");
        (await new ReferenceSetRepository(db).ListAsync("s-1", default)).Single().Files.Should().Be(2,
            "a reads row is never one of the set's files");
    }

    [Fact]
    public async Task ReferenceReadRepository_AddAsync_ASetNoLongerHeld_StoresNothing()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var reads = new ReferenceReadRepository(db);

        (await reads.AddAsync("s-1", "gone", ["app/a.cs"], default)).Should().BeNull();
        (await reads.ReadsAsync("s-1", default)).Should().BeEmpty();
    }

    // Two replicas that read the same rows and each added a merged row before either deleted: the read is the union.
    [Fact]
    public async Task ReferenceReadRepository_TwoRowsForOneSet_ReadsTheUnion()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/a.cs")], default);
        db.Add(Reads(set.SetId, "app/a.cs"));
        db.Add(Reads(set.SetId, "app/b.cs\napp/c.cs"));
        await db.SaveChangesAsync();

        (await new ReferenceReadRepository(db).ReadsAsync("s-1", default))[set.SetId]
            .Should().BeEquivalentTo(["app/a.cs", "app/b.cs", "app/c.cs"]);
    }

    [Fact]
    public async Task ReferenceUploadDeletion_DeleteSet_RemovesItsReads()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync("s-1", [File("app/a.cs")], default);
        await new ReferenceReadRepository(db).AddAsync("s-1", set.SetId, ["app/a.cs"], default);

        await new ReferenceUploadDeletion(db, new ApprovedSeriesRepository(db)).DeleteSetAsync("s-1", set.SetId, default);

        (await db.Set<ReferenceFile>().AnyAsync(f => f.SetId == set.SetId)).Should().BeFalse();
    }

    private static ReferenceFile Reads(string setId, string paths) => new()
    {
        SessionId = "s-1", SetId = setId, Kind = ReferenceFileKind.Reads, RelativePath = "READS.txt",
        MediaType = "text/plain", Content = System.Text.Encoding.UTF8.GetBytes(paths),
    };

    private static ReferenceFile File(string path) => new() { RelativePath = path, MediaType = "text/plain", Content = [0x61] };
}
