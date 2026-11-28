using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcj: Changed in this series — every revision after the first of each member ticket's set, read
/// off the ticket branch, newest first, whoever wrote it; nothing stored beside it.</summary>
public sealed class SeriesRunChangesReaderTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private const string EditCause = "approved in design conversation conv-sr-1";
    private readonly TicketStoreHarness _h = new(services => services
        .AddSingleton<ISeriesRunStore, SeriesRunRepository>().AddTransient<SeriesRunChangesReader>());

    public SeriesRunChangesReaderTests() => _h.Config.Projects["alpha"] = new()
    {
        Name = "alpha",
        Tracker = new AgentSmith.Contracts.Models.Configuration.TrackerConnection
        {
            Name = "gh", Type = AgentSmith.Contracts.Models.Configuration.TrackerType.GitHub,
        },
        Repos = [new AgentSmith.Contracts.Models.Configuration.RepoConnection { Name = "api", DefaultBranch = "main" }],
    };

    [Fact]
    public async Task ReadAsync_AnEditSeriesAppendAndAnInRunRevision_AreNotesWithTheirCause()
    {
        await OpenAsync("1", "2");
        await FileAsync("1");
        await FileAsync("2");
        await AppendThroughTheNextAttemptAsync("1");
        await ReviseInRunAsync("1", "the migration needs a rollback step");
        (await BranchSetAsync("1")).Specs.Should().HaveCount(2, "the appended spec is on the branch");

        var view = (await ReadAsync("sr-1"))!;

        view.Changes.Select(c => (c.TicketId, c.Revision, c.Cause)).Should().Equal(
            [("1", 3, "revised in run run-7: the migration needs a rollback step"), ("1", 2, EditCause)]);
        view.Unread.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_ARefusedEdit_WroteNoRevision_AndShowsNoNote()
    {
        // A refused edit_series returns before anything is recorded, so the branch keeps the filing's revision alone.
        await OpenAsync("1");
        await FileAsync("1");

        (await ReadAsync("sr-1"))!.Changes.Should().BeEmpty("the filing's own first revision is not a change");
    }

    [Fact]
    public async Task ReadAsync_NotesOfSeveralTickets_SortNewestFirst()
    {
        await OpenAsync("1", "2");
        await FileAsync("1");
        await FileAsync("2");
        await AppendThroughTheNextAttemptAsync("1");
        await ReviseInRunAsync("2", "a step was missing");
        await ReviseInRunAsync("1", "the rollback needs a test");

        var changes = (await ReadAsync("sr-1"))!.Changes;

        changes.Select(c => (c.TicketId, c.Revision)).Should().Equal([("1", 3), ("2", 2), ("1", 2)]);
        changes.Select(c => c.At).Should().BeInDescendingOrder();
    }

    [Fact]
    // 2026-11-28-db88a: the changes are the stored set's revisions; a branch the host cannot read hides none of them.
    public async Task ReadAsync_ABranchThatCannotBeRead_StillShowsTheStoredRevisions()
    {
        await OpenAsync("1");
        await FileAsync("1");
        await AppendThroughTheNextAttemptAsync("1");
        _h.Branches.ReadThrows = new HttpRequestException("503 from the host");

        var view = (await ReadAsync("sr-1"))!;

        view.Changes.Should().ContainSingle().Which.TicketId.Should().Be("1");
        view.Unread.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_UnknownSeries_IsNull()
    {
        (await ReadAsync("none")).Should().BeNull();
    }

    [Fact]
    public async Task ChangesAsync_AnUnknownSeries_Is404_AndAKnownOneIsTheView()
    {
        await OpenAsync("1");
        await using var scope = _h.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<SeriesRunChangesReader>();

        (await SeriesRunEndpoints.ChangesAsync("none", reader, default)).Should().BeOfType<NotFound>();
        (await SeriesRunEndpoints.ChangesAsync("sr-1", reader, default)).Should().BeOfType<Ok<SeriesRunChangesView>>();
    }

    public void Dispose() => _h.Dispose();

    private async Task<SeriesRunChangesView?> ReadAsync(string id)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SeriesRunChangesReader>().ReadAsync(id, default);
    }

    private Task OpenAsync(params string[] ids) =>
        _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(new SeriesRunRecord("sr-1", "conv-sr-1", "alpha", "operator", true,
            SeriesRunStates.Running, [.. ids.Select(id => new SeriesTicket(id, $"SCRUM-{id}", $"Ticket {id}", false, T))], T), default);

    private Task FileAsync(string ticketId) =>
        _h.AddAsync(new TicketBranchSet(ticketId, [BranchSpec($"2026-10-13-1234{(char)('a' + int.Parse(ticketId) - 1)}")]));

    // edit_series records an approval; the next attempt applies it and writes the revision the way every run does.
    private async Task AppendThroughTheNextAttemptAsync(string ticketId)
    {
        var set = await BranchSetAsync(ticketId);
        var record = new SpecApprovalRecord(set.Key, set with { Approval = new SpecApproval(T, "conv-sr-1", "operator") }, ["api"], "gh", "api");
        var added = BranchSpec($"2026-10-13-1234{(char)('m' + int.Parse(ticketId))}");
        var appended = set with { Specs = [.. set.Specs, added] };
        var ticket = new AgentSmith.Domain.Entities.Ticket(new TicketId(ticketId), "t", "b", null, "open", "github", []);
        Put(set, SpecRevisionHeader.Finalize(appended, new SpecSetReadResult(set, "sha-1"), ApprovedSetHandoff.CauseOf(record), ticket,
            modelRan: false), new AgentSmith.Contracts.Providers.RepoFile($"{SeriesPaths.Planned}/{added.SpecId}-label.yaml", added.Draft.Yaml));
    }

    // revise_spec in a run: the run's one revision publisher appends the revision its cause names.
    private async Task ReviseInRunAsync(string ticketId, string reason)
    {
        var set = await BranchSetAsync(ticketId);
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.SpecSet, set);
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.RunId, "run-7");
        SpecSet? published = null;
        var publisher = new Moq.Mock<ISpecSetPublisher>(Moq.MockBehavior.Strict);
        publisher.Setup(p => p.PublishAsync(pipeline, Moq.It.IsAny<string>(), Moq.It.IsAny<SeriesScope>(), Moq.It.IsAny<SpecSet>(),
                Moq.It.IsAny<IReadOnlyList<IgnoredInstruction>>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<AgentSmith.Contracts.Commands.PipelineContext, string, SeriesScope, SpecSet, IReadOnlyList<IgnoredInstruction>, CancellationToken>(
                (ctx, _, _, revised, _, _) => { published = revised; ctx.Set(AgentSmith.Contracts.Commands.ContextKeys.SpecRevisionSha, "sha-2"); })
            .Returns(Task.FromResult(CommandResult.Ok("published")));
        var revision = new AgentSmith.Application.Services.Specs.Review.SpecReviewRevision(publisher.Object, new AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore());
        var result = await revision.PublishAsync(pipeline, [set.Specs[0].Draft],
            AgentSmith.Application.Services.Specs.Revision.SpecRevisionWriter.Cause(pipeline, reason), default);
        result.IsSuccess.Should().BeTrue(result.Message);
        Put(set, published!);
    }

    private async Task<SpecSet> BranchSetAsync(string ticketId)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<TicketBranchSpecs>().ReadAsync("alpha", ticketId, default)).Set!;
    }

    // 2026-11-28-db88a: the revised set is stored on the ticket's row, as the run's publish leaves it; its new spec files
    // are the branch's record.
    private void Put(SpecSet before, SpecSet after, params AgentSmith.Contracts.Providers.RepoFile[] specFiles)
    {
        _h.Pointers.SaveSetAsync("alpha", after, default).GetAwaiter().GetResult();
        if (specFiles.Length > 0) _h.Branches.Seed("api", "agent-smith/" + before.Key.Split('-')[^1], specFiles);
    }
}
