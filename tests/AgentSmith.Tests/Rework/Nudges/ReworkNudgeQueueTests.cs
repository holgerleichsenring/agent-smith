using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Rework.Nudges;

/// <summary>2026-10-08-0781: the durable nudge queue — the release transaction and the claim protocol.</summary>
public sealed class ReworkNudgeQueueTests : IDisposable
{
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero) };
    private readonly ServerStateStore _store;

    public ReworkNudgeQueueTests() => _store = new ServerStateStore(_clock);

    public void Dispose() => _store.Dispose();

    private ReworkNudgeRepository Repo() => _store.Services.CreateScope().ServiceProvider.GetRequiredService<ReworkNudgeRepository>();

    private ActiveRunRepository Leases() => new(_store.Context(), new SqliteUniqueViolationTranslator(), _clock,
        NullLogger<ActiveRunRepository>.Instance);

    private List<ReworkNudge> Rows() => [.. _store.Context().Set<ReworkNudge>()];

    [Fact]
    public async Task Release_NudgeAndDelete_OneTransaction()
    {
        var leases = Leases();
        await leases.TryClaimAsync("p", new TicketId("7"), CancellationToken.None);
        await Leases().AttachRunAsync("p", new TicketId("7"), "run-1", null, CancellationToken.None);

        await Leases().ReleaseAsync("p", new TicketId("7"), "run-other", CancellationToken.None);
        Rows().Should().BeEmpty("a refused release leaves no nudge");

        await Leases().ReleaseAsync("p", new TicketId("7"), "run-1", CancellationToken.None);
        Rows().Should().ContainSingle(n => n.Project == "p" && n.TicketId == "7" && n.Origin == (int)ReworkNudgeOrigin.RunEnd);
        _store.Context().Set<ActiveRun>().Should().BeEmpty();
    }

    [Fact]
    public async Task Worker_CrashedClaim_ReclaimedAfterExpiry()
    {
        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Ticket), CancellationToken.None);
        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().ContainSingle();

        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().BeEmpty("the first claim still holds it");
        _clock.Now += ReworkNudgeRepository.ClaimLease + TimeSpan.FromSeconds(1);

        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().ContainSingle("a crashed claim expires");
    }

    [Fact]
    public async Task Worker_MergeDuringHandling_ServedNext()
    {
        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.RunEnd), CancellationToken.None);
        var claimed = (await Repo().ClaimDueAsync(10, CancellationToken.None)).Single();
        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.Ticket, Channel: ReworkChannel.Ticket), CancellationToken.None);

        await Repo().FinishAsync(claimed, CancellationToken.None);

        var row = Rows().Single();
        row.Origin.Should().Be((int)ReworkNudgeOrigin.Ticket, "a ticket origin wins the merge");
        row.Channel.Should().Be("Ticket");
        row.ClaimToken.Should().BeNull();
        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().ContainSingle("the merged request is served next");
    }

    [Fact]
    public async Task Merge_RunEnd_NeverPullsDueForward()
    {
        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.RunEnd, Delay: TimeSpan.FromMinutes(1)), CancellationToken.None);
        await Repo().EnqueueAsync(new ReworkNudgeRequest("p", "7", ReworkNudgeOrigin.RunEnd), CancellationToken.None);

        (await Repo().ClaimDueAsync(10, CancellationToken.None)).Should().BeEmpty();
        Rows().Single().Generation.Should().Be(2);
    }

    [Fact]
    public async Task Worker_DeletedRun_ActsBeforeDeleteNotServed()
    {
        using (var ctx = _store.Context())
        {
            ctx.Add(new Run { Id = "run-1", Project = "p", TicketId = "7", Pipeline = "code", Status = "success",
                StartedAt = _clock.Now.AddHours(-1), FinishedAt = _clock.Now.AddMinutes(-30) });
            await ctx.SaveChangesAsync();
        }
        var before = DateTimeOffset.UtcNow;

        using (var ctx = _store.Context())
            await new RunDeletionRepository(ctx, new SqliteUniqueViolationTranslator()).DeleteAsync("run-1", CancellationToken.None);

        using var read = _store.Context();
        var mark = await new ReworkLedgerRepository(read, new SqliteUniqueViolationTranslator()).WatermarkAsync("p", "7", CancellationToken.None);
        mark.Should().NotBeNull();
        mark!.Value.Should().BeOnOrAfter(before.AddSeconds(-1), "every act up to the delete is withheld");
    }
}
