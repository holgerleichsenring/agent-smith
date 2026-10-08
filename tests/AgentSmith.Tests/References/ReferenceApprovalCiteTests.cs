using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283df: an approval cites the conversation's uploaded websites by set id, the citation
/// survives the wire, and deleting the conversation keeps exactly the sets an approval cites.
/// </summary>
public sealed class ReferenceApprovalCiteTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SpecApprovalJson_References_RoundTrip()
    {
        var record = Record("s-1", ["set-a", "set-b"]);

        var back = SpecApprovalJson.Read(SpecApprovalJson.Write(record))!;

        back.References.Should().Equal("set-a", "set-b");
        back.CitedSets.Should().Equal("set-a", "set-b");
    }

    [Fact]
    public void SpecApprovalJson_RecordWrittenBeforeTheField_CitesNothing()
    {
        var json = SpecApprovalJson.Write(Record("s-1", null)).Replace("\"references\":null,", string.Empty);

        SpecApprovalJson.Read(json)!.CitedSets.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedPhaseSetRecorder_Record_CitesTheConversationsSetsAtApproval()
    {
        var references = new ReferenceSandboxFixture();
        var recorder = new ApprovedPhaseSetRecorder(ApprovedSetDoubles.Store(), references,
            TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

        var record = await recorder.RecordAsync(new ConversationState
        {
            JobId = ReferenceSandboxFixture.Conversation, ChannelId = "c", UserId = "u", Platform = "dashboard",
            Project = "sample", TicketId = string.Empty, StartedAt = Noon,
        }, new ResolvedProject
        {
            Name = "sample", Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
            Repos = [new RepoConnection { Name = "sample-api" }],
        }, "19106", ApprovedSetDoubles.Series(new PhaseDraft("p1", "Do it", "spec: p1", []) { Done = ["done"] }), "Do it", CancellationToken.None);

        record.CitedSets.Should().Equal(ReferenceSandboxFixture.SetId);
    }

    [Fact]
    public async Task ConversationDelete_CitedSet_Survives_UncitedSetIsSwept()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var cited = await sets.AddAsync("s-1", [File("site/index.html")], CancellationToken.None);
        var uncited = await sets.AddAsync("s-1", [File("draft/index.html")], CancellationToken.None);
        var elsewhere = await sets.AddAsync("s-2", [File("other/index.html")], CancellationToken.None);
        await new ReferenceFileRepository(db).AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);
        var approvals = new ApprovedSeriesRepository(db);
        await approvals.SaveAsync(Record("s-1", [cited.SetId]), CancellationToken.None);
        var deleter = new SpecDialogConversationDeleter(db, new SpecDialogSessionRepository(db),
            new DialogueAnswerRepository(db, new SqliteUniqueViolationTranslator()), new ReferenceFileRepository(db), approvals);

        await deleter.DeleteAsync("s-1", CancellationToken.None);

        var left = await db.Set<ReferenceFile>().AsNoTracking().Select(f => new { f.SessionId, f.SetId, f.Kind }).ToListAsync();
        left.Select(f => f.SetId).Should().BeEquivalentTo([cited.SetId, elsewhere.SetId],
            "the cited set is what the filed ticket's run builds against; the uncited one and the image go");
        left.Should().NotContain(f => f.SetId == uncited.SetId).And.NotContain(f => f.Kind == ReferenceFileKind.Image);
        (await approvals.CitedSetsAsync("s-1", CancellationToken.None)).Should().Equal(cited.SetId);
    }

    // 2026-10-08-e8b9k: an approval cites the conversation's images beside its sets.
    [Fact]
    public void SpecApprovalJson_Images_RoundTrip()
    {
        var record = Record("s-1", ["set-a"]) with { Images = ["img-1"] };

        SpecApprovalJson.Read(SpecApprovalJson.Write(record))!.CitedImages.Should().Equal("img-1");
    }

    [Fact]
    public void SpecApprovalJson_OldRecord_CitesNoImages()
    {
        var json = SpecApprovalJson.Write(Record("s-1", ["set-a"])).Replace("\"images\":null,", string.Empty);

        SpecApprovalJson.Read(json)!.CitedImages.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedPhaseSetRecorder_ConversationWithImage_CitesIt()
    {
        var references = new WithImage();
        var recorder = new ApprovedPhaseSetRecorder(ApprovedSetDoubles.Store(), references,
            TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

        var record = await recorder.RecordAsync(new ConversationState
        {
            JobId = ReferenceSandboxFixture.Conversation, ChannelId = "c", UserId = "u", Platform = "dashboard",
            Project = "sample", TicketId = string.Empty, StartedAt = Noon,
        }, new ResolvedProject
        {
            Name = "sample", Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
            Repos = [new RepoConnection { Name = "sample-api" }],
        }, "19106", ApprovedSetDoubles.Series(new PhaseDraft("p1", "Do it", "spec: p1", []) { Done = ["done"] }), "Do it", CancellationToken.None);

        record.CitedImages.Should().Equal("img-1");
        record.CitedSets.Should().Equal(ReferenceSandboxFixture.SetId);
    }

    [Fact]
    public async Task ConversationDelete_CitedImage_Survives()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var cited = await new ReferenceFileRepository(db).AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);
        await new ReferenceFileRepository(db).AddAsync(LegacyAttachmentCopyTests.Image("s-1"), CancellationToken.None);
        var approvals = new ApprovedSeriesRepository(db);
        await approvals.SaveAsync(Record("s-1", []) with { Images = [cited.SetId] }, CancellationToken.None);

        await Deleter(db, approvals).DeleteAsync("s-1", CancellationToken.None);

        (await db.Set<ReferenceFile>().AsNoTracking().Select(f => f.SetId).ToListAsync()).Should().Equal(cited.SetId);
    }

    [Fact]
    public async Task ConversationDelete_CitedLegacyCopiedImage_SurvivesOrphanSweep()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        var id = await store.AddLegacyAsync("s-1", [0x89, 0x50, 0x4E, 0x47], Noon);
        await using (var copy = store.Context()) await store.Copy(copy).CopyBatchAsync(CancellationToken.None);
        await using var db = store.Context();
        var setId = await db.Set<ReferenceFile>().AsNoTracking().Where(f => f.Id == id).Select(f => f.SetId).SingleAsync();
        var approvals = new ApprovedSeriesRepository(db);
        await approvals.SaveAsync(Record("s-1", []) with { Images = [setId] }, CancellationToken.None);

        await Deleter(db, approvals).DeleteAsync("s-1", CancellationToken.None);
        await using (var sweep = store.Context()) await store.Copy(sweep).RemoveOrphansAsync(CancellationToken.None);

        await using var read = store.Context();
        (await read.Set<ReferenceFile>().AsNoTracking().CountAsync(f => f.Id == id)).Should().Be(1,
            "the legacy row of a cited image stays, so the leader's sweep keeps its copy");
    }

    private static SpecDialogConversationDeleter Deleter(AgentSmith.Infrastructure.Persistence.AgentSmithDbContext db, ApprovedSeriesRepository approvals) =>
        new(db, new SpecDialogSessionRepository(db),
            new DialogueAnswerRepository(db, new SqliteUniqueViolationTranslator()), new ReferenceFileRepository(db), approvals);

    private sealed class WithImage : AgentSmith.Contracts.Sandbox.IReferenceSetReader
    {
        public Task<IReadOnlyList<AgentSmith.Contracts.Sandbox.ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentSmith.Contracts.Sandbox.ReferenceSetFile>>([]);

        public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([ReferenceSandboxFixture.SetId]);

        public Task<IReadOnlyList<string>> ImageSetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["img-1"]);
    }

    private static SpecApprovalRecord Record(string conversation, IReadOnlyList<string>? references) =>
        new("azuredevops-19106",
            new SpecSet("azuredevops-19106", [], SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(Noon, conversation, "person")),
            ["sample-api"], "tracker", "sample-api", "19106", references);

    private static ReferenceFile File(string path) => new()
    {
        RelativePath = path, MediaType = "text/html", Content = "<p>x</p>"u8.ToArray(),
    };
}
