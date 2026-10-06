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
        }, "19106", [new PhaseDraft("p1", "Do it", "spec: p1", []) { Done = ["done"] }], CancellationToken.None);

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
        var approvals = new ApprovedSpecSetRepository(db);
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
