using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-01-283da: until the legacy table is dropped, a conversation's images are the
/// reference images PLUS the legacy rows not copied yet — each once, sites never, oldest first.
/// </summary>
public sealed class ReferenceFileRepositoryTests
{
    private static readonly DateTimeOffset Then = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task ReferenceFileRepository_Recent_OnlyImagesAndUncopiedLegacyRowsCount()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        var copied = await store.AddLegacyAsync("s-1", Png, Then);
        await using (var db = store.Context()) await store.Copy(db).CopyBatchAsync(CancellationToken.None);
        var uncopied = await store.AddLegacyAsync("s-1", [0x01], Then.AddMinutes(1));
        await using var context = store.Context();
        var files = new ReferenceFileRepository(context);
        var born = await files.AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);
        await files.AddAsync(Site("s-1"), CancellationToken.None);
        await files.AddAsync(LegacyAttachmentCopyTests.Image("s-other"), CancellationToken.None);

        var (existing, recent) = await files.RecentImagesAsync("s-1", 2, CancellationToken.None);

        existing.Should().Be(3, "the copied row counts once and the site file not at all");
        recent.Select(i => i.Id).Should().Equal(uncopied, born.Id);
        recent[0].Content.Should().Equal([0x01], "a legacy row's base64 is decoded on the way out");
    }

    [Fact]
    public async Task ReferenceFileRepository_ListImages_OrdersBothTablesByTheirMoment()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var context = store.Context();
        var files = new ReferenceFileRepository(context);
        var born = await files.AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);
        // An older replica writes a legacy row after the new one: a lower id, a later moment.
        var late = await store.AddLegacyAsync("s-1", Png, DateTimeOffset.UtcNow.AddMinutes(5));

        var listed = await files.ListImagesAsync("s-1", CancellationToken.None);

        listed.Select(i => i.Id).Should().Equal(born.Id, late);
    }

    [Fact]
    public async Task ReferenceFileRepository_Get_ASiteFileOrAMissingId_IsNoImage()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var context = store.Context();
        var files = new ReferenceFileRepository(context);
        var site = await files.AddAsync(Site("s-1"), CancellationToken.None);

        (await files.GetAsync(site.Id, CancellationToken.None)).Should().BeNull();
        (await files.GetAsync(424242, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ReferenceFileRepository_DeleteBySession_ClearsBothTablesForThatConversationOnly()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await store.AddLegacyAsync("s-1", Png, Then);
        await store.AddLegacyAsync("s-2", Png, Then);
        await using var context = store.Context();
        var files = new ReferenceFileRepository(context);
        await files.AddAsync(Site("s-1"), CancellationToken.None);

        (await files.DeleteBySessionAsync("s-1", new HashSet<string>(), CancellationToken.None)).Should().Be(2);

        (await context.Set<ReferenceFile>().CountAsync()).Should().Be(0);
        (await context.Set<SpecDialogAttachment>().Select(a => a.SessionId).ToListAsync()).Should().Equal("s-2");
    }

    private static ReferenceFile Site(string sessionId) => new()
    {
        SessionId = sessionId, SetId = "set-1", Kind = ReferenceFileKind.Site, RelativePath = "css/site.css",
        MediaType = "text/css", Length = 3, Content = [0x61, 0x7B, 0x7D],
    };
}
