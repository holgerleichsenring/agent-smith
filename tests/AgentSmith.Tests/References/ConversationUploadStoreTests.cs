using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.References;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-08-e8b9g: a conversation's uploads over the REAL store — held to a byte cap, refused
/// as copies by content hash, removed one at a time unless an approval cites them, and read with
/// their sizes and citations.
/// </summary>
public sealed class ConversationUploadStoreTests
{
    private const string Session = "s-e8b9g";
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x07];

    [Fact]
    public async Task ReferenceUpload_OverConversationCap_RefusesNamingUsedAndCap()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        db.Add(Row(ReferenceFileKind.Site, "big/a.bin", [1], length: 99L * 1024 * 1024));
        await db.SaveChangesAsync();

        var refusal = await TestUploads.Admission(db).RefusalForSetAsync(
            Session, [("site/a.bin", new byte[2 * 1024 * 1024])], CancellationToken.None);

        refusal!.Status.Should().Be(StatusCodes.Status400BadRequest);
        refusal.Reason.Should().Contain("99 MB").And.Contain("2 MB").And.Contain("100 MB");
    }

    [Fact]
    public async Task ReferenceUpload_SameFilesAndPaths_Refused409NamingExisting()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        await new ReferenceSetRepository(db).AddAsync(Session, [File("site/index.html", "<h1>"), File("site/a.css", "a{}")], CancellationToken.None);

        var refusal = await TestUploads.Admission(db).RefusalForSetAsync(
            Session, [("site/a.css", "a{}"u8.ToArray()), ("site/index.html", "<h1>"u8.ToArray())], CancellationToken.None);

        refusal!.Status.Should().Be(StatusCodes.Status409Conflict);
        refusal.Reason.Should().Contain("'site'");
    }

    [Fact]
    public async Task ReferenceUpload_SameFilesOtherPath_IsStored()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        await new ReferenceSetRepository(db).AddAsync(Session, [File("site/index.html", "<h1>")], CancellationToken.None);

        (await TestUploads.Admission(db).RefusalForSetAsync(
            Session, [("copy/index.html", "<h1>"u8.ToArray())], CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ReferenceUpload_RowWithoutHash_IsNotMatched()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        db.Add(Row(ReferenceFileKind.Site, "site/index.html", "<h1>"u8.ToArray(), hash: false));
        await db.SaveChangesAsync();

        (await TestUploads.Admission(db).RefusalForSetAsync(
            Session, [("site/index.html", "<h1>"u8.ToArray())], CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ImageUpload_SameBytes_Refused409()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        db.Add(Row(ReferenceFileKind.Image, string.Empty, Png));
        await db.SaveChangesAsync();

        var refusal = await TestUploads.Admission(db).RefusalForImageAsync(Session, Png, CancellationToken.None);

        refusal!.Status.Should().Be(StatusCodes.Status409Conflict);
        (await TestUploads.Admission(db).RefusalForImageAsync(Session, [.. Png, 0x01], CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task LegacyAttachmentCopy_Copy_FillsContentSha256()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await store.AddLegacyAsync(Session, Png, Noon);

        await using (var db = store.Context()) await store.Copy(db).CopyBatchAsync(CancellationToken.None);

        await using var read = store.Context();
        (await read.Set<ReferenceFile>().AsNoTracking().SingleAsync()).ContentSha256.Should().Be(Png.Sha256Hex());
    }

    [Fact]
    public async Task DeleteReferenceSet_Uncited_RemovesFilesAndNote()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync(Session, [File("site/index.html", "<h1>")], CancellationToken.None);
        await new ReferenceNoteRepository(db).SetAsync(Session, set.SetId, "run it with npx serve", CancellationToken.None);

        (await Deletion(db).DeleteSetAsync(Session, set.SetId, CancellationToken.None)).Should().Be(ReferenceUploadRemoval.Removed);

        (await db.Set<ReferenceFile>().AsNoTracking().CountAsync()).Should().Be(0, "the set and its note go together");
    }

    [Fact]
    public async Task DeleteReferenceSet_CitedByApproval_Refused409()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var set = await new ReferenceSetRepository(db).AddAsync(Session, [File("site/index.html", "<h1>")], CancellationToken.None);
        await new ApprovedSeriesRepository(db).SaveAsync(Record([set.SetId]), CancellationToken.None);

        (await Deletion(db).DeleteSetAsync(Session, set.SetId, CancellationToken.None)).Should().Be(ReferenceUploadRemoval.Cited);

        (await db.Set<ReferenceFile>().AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteImage_CopiedLegacy_BothRowsGone()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        var id = await store.AddLegacyAsync(Session, Png, Noon);
        await using (var copy = store.Context()) await store.Copy(copy).CopyBatchAsync(CancellationToken.None);
        await using var db = store.Context();

        (await Deletion(db).DeleteImageAsync(Session, id, CancellationToken.None)).Should().Be(ReferenceUploadRemoval.Removed);

        (await db.Set<ReferenceFile>().AsNoTracking().CountAsync()).Should().Be(0);
        (await db.Set<SpecDialogAttachment>().AsNoTracking().CountAsync()).Should().Be(0, "else the next copy pass brings it back");
    }

    [Fact]
    public async Task DeleteImage_UncopiedLegacyId_Removed()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        var id = await store.AddLegacyAsync(Session, Png, Noon);
        await using var db = store.Context();

        (await Deletion(db).DeleteImageAsync(Session, id, CancellationToken.None)).Should().Be(ReferenceUploadRemoval.Removed);
        (await Deletion(db).DeleteImageAsync(Session, id, CancellationToken.None)).Should().Be(ReferenceUploadRemoval.NotFound);
    }

    [Fact]
    public async Task ApprovalCitations_SetRemovedBeforeReRead_IsNotCited()
    {
        var approvals = ApprovedSetDoubles.Store();
        var citations = new ApprovalCitations(approvals, new HeldSets([]));

        var record = await citations.SaveAsync(Record(["set-gone"]), Session, CancellationToken.None);

        record.CitedSets.Should().BeEmpty("the set was removed between the read and the save");
        (await approvals.GetAsync(record.Tracker, record.Key, CancellationToken.None))!.CitedSets.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecDialogView_Uploads_CarryBytesCitedAndUsage()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var cited = await new ReferenceSetRepository(db).AddAsync(Session, [File("site/index.html", "<h1>")], CancellationToken.None);
        await new ReferenceSetRepository(db).AddAsync(Session, [File("draft/index.html", "<h2>")], CancellationToken.None);
        db.Add(Row(ReferenceFileKind.Image, string.Empty, Png));
        await db.SaveChangesAsync();
        await new ApprovedSeriesRepository(db).SaveAsync(Record([cited.SetId]), CancellationToken.None);

        var read = await TestUploads.Over(db).ReadAsync(Session, CancellationToken.None);

        read.References.Select(r => r.Cited).Should().Equal(true, false);
        read.Images.Should().ContainSingle().Which.Bytes.Should().Be(Png.Length);
        read.Bytes.Should().Be(4 + 4 + Png.Length);
    }

    [Fact]
    public async Task SpecDialogView_LegacyOnlyImage_BytesNullNotCounted()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await store.AddLegacyAsync(Session, Png, Noon);
        await using var db = store.Context();

        var read = await TestUploads.Over(db).ReadAsync(Session, CancellationToken.None);

        read.Images.Should().ContainSingle().Which.Bytes.Should().BeNull();
        read.Bytes.Should().Be(0);
    }

    [Fact]
    public async Task SqliteMigrate_ReferenceFiles_HasContentSha256()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();

        var columns = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('ReferenceFiles')").ToListAsync();

        columns.Should().Contain("ContentSha256");
    }

    private static ReferenceUploadDeletion Deletion(AgentSmithDbContext db) => new(db, new ApprovedSeriesRepository(db));

    private static ReferenceFile File(string path, string text) => new()
    {
        RelativePath = path, MediaType = "text/html", Content = System.Text.Encoding.UTF8.GetBytes(text),
    };

    private static ReferenceFile Row(string kind, string path, byte[] content, long? length = null, bool hash = true) => new()
    {
        SessionId = Session, SetId = Guid.NewGuid().ToString("N"), Kind = kind, RelativePath = path,
        MediaType = "image/png", Content = content, Length = length ?? content.Length,
        ContentSha256 = hash ? content.Sha256Hex() : null,
    };

    private static SpecApprovalRecord Record(IReadOnlyList<string> references) =>
        new("azuredevops-19106",
            new SpecSet("azuredevops-19106", [], SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(Noon, Session, "person")),
            ["sample-api"], "tracker", "sample-api", "19106", references);

    private sealed class HeldSets(IReadOnlyList<string> ids) : IReferenceSetReader
    {
        public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReferenceSetFile>>([]);

        public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(ids);
    }
}
