using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-01-283da: the legacy image rows are copied into the reference files under their own
/// ids, bytes and moments, at most once whatever interleaves, and a copy outlives its original
/// only until the next sweep. On SQLite always; on SQL Server whenever a server is named.
/// </summary>
public sealed class LegacyAttachmentCopyTests
{
    private static readonly DateTimeOffset Then = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x2A];

    public static TheoryData<string> Providers() => ReferenceFileStore.Providers();

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task LegacyAttachmentCopy_KeepsIdBytesAndCreatedAt(string provider)
    {
        await using var store = await ReferenceFileStore.OpenAsync(provider);
        var first = await store.AddLegacyAsync("s-1", Png, Then);
        var second = await store.AddLegacyAsync("s-1", [.. Png, 0x01], Then.AddMinutes(1));

        await using (var db = store.Context())
            (await store.Copy(db).CopyBatchAsync(CancellationToken.None)).Should().Be(2);

        await using var read = store.Context();
        var copies = await read.Set<ReferenceFile>().AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        copies.Select(f => f.Id).Should().Equal(first, second);
        copies[0].Content.Should().Equal(Png);
        copies[0].Length.Should().Be(Png.Length);
        copies[0].Kind.Should().Be(ReferenceFileKind.Image);
        copies[0].SessionId.Should().Be("s-1");
        copies.Select(f => f.CreatedAt).Should().Equal(Then, Then.AddMinutes(1));
    }

    /// <summary>The done line: an image uploaded before the upgrade is served byte-identical
    /// under its old id after it, before and after the copy has run — and a new one is born
    /// above every id the legacy table could hold.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task LegacyImage_BeforeAndAfterTheCopy_IsServedByteIdenticalUnderItsOldId(string provider)
    {
        await using var store = await ReferenceFileStore.OpenAsync(provider);
        var old = await store.AddLegacyAsync("s-1", Png, Then);

        await using (var before = store.Context())
            (await new ReferenceFileRepository(before).GetAsync(old, CancellationToken.None))!.Content.Should().Equal(Png);
        await using (var db = store.Context()) await store.Copy(db).CopyBatchAsync(CancellationToken.None);

        await using var after = store.Context();
        var files = new ReferenceFileRepository(after);
        (await files.GetAsync(old, CancellationToken.None))!.Content.Should().Equal(Png);
        var born = await files.AddAsync(Image("s-1"), CancellationToken.None);
        born.Id.Should().BeGreaterThanOrEqualTo(ReferenceFileIdentity.Seed, "one id space, legacy below the seed");
    }

    [Fact]
    public async Task LegacyAttachmentCopy_TwoCopiersAtOnce_EachRowOnce()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        var ids = new List<long>();
        for (var i = 0; i < 3; i++) ids.Add(await store.AddLegacyAsync("s-1", Png, Then.AddSeconds(i)));

        // The rival copies everything after the first copier read its batch and before it wrote.
        await using var rival = store.Context();
        await using var db = store.Context(new RaceBeforeFirstSave(
            () => store.Copy(rival).CopyBatchAsync(CancellationToken.None)));
        var found = await store.Copy(db).CopyBatchAsync(CancellationToken.None);

        found.Should().Be(3, "the first copier read three uncopied rows");
        await using var read = store.Context();
        (await read.Set<ReferenceFile>().Select(f => f.Id).OrderBy(id => id).ToListAsync())
            .Should().Equal(ids, "each row is copied once; the loser's inserts are key violations, skipped");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task LegacyAttachmentCopy_LegacyRowDeletedAfterCopy_RemovesTheCopy(string provider)
    {
        await using var store = await ReferenceFileStore.OpenAsync(provider);
        var gone = await store.AddLegacyAsync("s-old", Png, Then);
        var kept = await store.AddLegacyAsync("s-kept", Png, Then);
        await using var db = store.Context();
        var copy = store.Copy(db);
        await copy.CopyBatchAsync(CancellationToken.None);
        await new ReferenceFileRepository(db).AddAsync(Image("s-new"), CancellationToken.None);

        // An older replica deletes its conversation from the only table it knows.
        await db.Set<SpecDialogAttachment>().Where(a => a.Id == gone).ExecuteDeleteAsync();
        (await copy.RemoveOrphansAsync(CancellationToken.None)).Should().Be(1);

        var left = await db.Set<ReferenceFile>().AsNoTracking().ToListAsync();
        left.Select(f => f.Id).Should().Contain(kept).And.NotContain(gone);
        left.Should().Contain(f => f.SessionId == "s-new", "a row born here is never an orphan");
    }

    /// <summary>An archive holding only copied legacy images must not pull the identity below
    /// the seed, or the next new image would be born in the legacy range.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task IdentitySequenceAdvancer_KeysBelowTheSeed_KeepTheReferenceIdentityAboveIt(string provider)
    {
        await using var store = await ReferenceFileStore.OpenAsync(provider);
        await using var db = store.Context();
        var advancer = new IdentitySequenceAdvancer(
            new GeneratedKeyProperty(), Microsoft.Extensions.Logging.Abstractions.NullLogger<IdentitySequenceAdvancer>.Instance);

        await advancer.AdvanceAsync(db, db.Model.FindEntityType(typeof(ReferenceFile))!, 57, CancellationToken.None);
        var born = await new ReferenceFileRepository(db).AddAsync(Image("s-1"), CancellationToken.None);

        born.Id.Should().BeGreaterThanOrEqualTo(ReferenceFileIdentity.Seed);
    }

    internal static ReferenceFile Image(string sessionId) => new()
    {
        SessionId = sessionId, SetId = Guid.NewGuid().ToString("N"), Kind = ReferenceFileKind.Image,
        MediaType = "image/png", Length = Png.Length, Content = Png,
    };
}
