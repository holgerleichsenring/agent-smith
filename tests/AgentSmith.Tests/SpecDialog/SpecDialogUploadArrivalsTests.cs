using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Persistence.ReferenceFiles;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-04-96a0b: a set stored while a turn ran is named by its address and file count; nothing else is.</summary>
public sealed class SpecDialogUploadArrivalsTests
{
    private static readonly ConversationState State = new()
    {
        JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a",
        Platform = "dashboard", TicketId = string.Empty, StartedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task SpecDialogUploadArrivals_ASetStoredDuringTheTurn_IsNamedWithItsAddressAndCount()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var held = await sets.AddAsync("s-1", [File("site/index.html")], CancellationToken.None);
        await sets.AddAsync("s-1", [File("app/a.cs"), File("app/b.cs")], CancellationToken.None);

        var notice = await new SpecDialogUploadArrivals(sets).NoticeAsync(
            State, Result(new AnswerOutcome { Uploads = [new UploadProvenance("reference:site", held.SetId)] }),
            CancellationToken.None);

        notice.Should().Be("reference:app (2 files) was uploaded while this answer was being written, so the answer "
            + "did not read it. The next answer can read it.");
    }

    [Fact]
    public async Task SpecDialogUploadArrivals_AProposalTurn_SaysApprovalDoesNotReadIt()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        await sets.AddAsync("s-1", [File("app/a.cs")], CancellationToken.None);

        var notice = await new SpecDialogUploadArrivals(sets).NoticeAsync(
            State, Result(new BugOutcome(new BugTicketDraft("t", "b", null))), CancellationToken.None);

        notice.Should().StartWith("reference:app (1 file) was uploaded")
            .And.EndWith("Approving this proposal does not read it.");
    }

    [Fact]
    public async Task SpecDialogUploadArrivals_EverySetWasHeld_ReturnsNull()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        var held = await sets.AddAsync("s-1", [File("site/index.html")], CancellationToken.None);

        var notice = await new SpecDialogUploadArrivals(sets).NoticeAsync(
            State, Result(new AnswerOutcome { Uploads = [new UploadProvenance("reference:site", held.SetId)] }),
            CancellationToken.None);

        notice.Should().BeNull();
    }

    [Fact]
    public async Task SpecDialogUploadArrivals_AFailedTurn_ReturnsNull()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var db = store.Context();
        var sets = new ReferenceSetRepository(db);
        await sets.AddAsync("s-1", [File("app/a.cs")], CancellationToken.None);

        var notice = await new SpecDialogUploadArrivals(sets).NoticeAsync(
            State, Result(new AnswerOutcome(), SpecDialogTurnKind.Failure), CancellationToken.None);

        notice.Should().BeNull("a failed turn's reply is a failure note, not an answer that missed a file");
    }

    private static SpecDialogTurnResult Result(OutcomeProposal outcome, SpecDialogTurnKind? kind = null) =>
        SpecDialogTurnResult.On("dashboard", "the answer", outcome, kind);

    private static ReferenceFile File(string path) => new() { RelativePath = path, MediaType = "text/plain", Content = [0x61] };
}
