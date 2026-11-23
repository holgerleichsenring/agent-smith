using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Holds;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dci: over a real migrated store — a model wait past holds.escalate_after_minutes is an admin hold
/// that is Stuck once and on the admin's Needs attention beside a precondition stop; one under it is nobody's and says
/// since when; the capacity queue's arrival is a queued ticket's since.</summary>
public sealed class TicketHoldEscalationListTests : IDisposable
{
    private static readonly DateTimeOffset Now = T.AddHours(3); // the harness clock
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task List_AModelWaitPastTheMinutes_IsStuckOnce_AsAnAdminHold_OneUnderThemIsNobodys()
    {
        await _h.AddAsync(Run("r-1", "41", status: RunStatuses.Running), Wait("r-1", Now.AddMinutes(-61)),
            Run("r-2", "42", status: RunStatuses.Running), Wait("r-2", Now.AddMinutes(-59)));

        var list = await ListAsync();

        var late = list.Tickets.Single(t => t.TicketId == "41");
        (late.Bucket, late.Since, late.Hold!.Addressee, late.Hold.Case).Should().Be(
            (TicketBucket.Stuck, Now.AddMinutes(-61), TicketHoldAddressees.Admin, TicketHoldCases.ModelWait));
        late.Hold.Escalation.Should().Be(new TicketHoldEscalation(60, "holds.escalate_after_minutes"));
        var waiting = list.Tickets.Single(t => t.TicketId == "42");
        (waiting.Bucket, waiting.Hold!.Addressee, waiting.Hold.Case, waiting.Hold.Since).Should().Be(
            (TicketBucket.Working, TicketHoldAddressees.Nobody, TicketHoldCases.ModelWait, Now.AddMinutes(-59)));
        (list.Counts.Stuck, list.Counts.AdminHolds, list.Counts.NeedsYou, list.Counts.Working).Should().Be((1, 1, 0, 1),
            "the escalated ticket is Stuck once and an admin hold once, not a person's move and no longer working");
    }

    [Fact]
    public async Task List_EscalationOff_LeavesTheLongWaitNobodys()
    {
        var config = new AgentSmith.Contracts.Models.Configuration.AgentSmithConfig
        {
            Projects = _h.Config.Projects, Holds = new() { EscalateAfterMinutes = 0 },
        };
        _h.Loader.Setup(l => l.LoadConfig(Moq.It.IsAny<string>())).Returns(config);
        await _h.AddAsync(Run("r-1", "41", status: RunStatuses.Running), Wait("r-1", Now.AddDays(-2)));

        (await ListAsync()).Tickets.Single().Hold!.Addressee.Should().Be(TicketHoldAddressees.Nobody);
    }

    [Fact]
    public async Task List_AQueuedTicket_WaitsSinceItsArrivalInTheCapacityQueue()
    {
        var queued = Run("r-1", "41", status: RunStatuses.Queued);
        await _h.AddAsync(queued, Queue("41", "r-1", Now.AddMinutes(-10)));

        var hold = (await ListAsync()).Tickets.Single().Hold!;

        (hold.Addressee, hold.Case, hold.Since).Should().Be((TicketHoldAddressees.Nobody, TicketHoldCases.Queued, Now.AddMinutes(-10)));
    }

    [Fact]
    public async Task NeedsAttention_ListsEscalatedAndPreconditionAdminHolds_LongestHeldFirst()
    {
        var stopped = Run("r-3", "43", status: RunStatuses.Failed);
        stopped.EndKind = RunEndKinds.ImageUntrusted;
        await _h.AddAsync(Run("r-1", "41", status: RunStatuses.Running), Wait("r-1", Now.AddMinutes(-90)),
            Run("r-2", "42", status: RunStatuses.Running), stopped, Run("r-4", "44", status: RunStatuses.Failed));

        await using var scope = _h.Services.CreateAsyncScope();
        var view = await scope.ServiceProvider.GetRequiredService<AdminHoldsReader>().ReadAsync(ProjectScope.Single("alpha"), default);

        view.Holds.Select(h => (h.TicketId, h.Hold.Case)).Should().Equal(
            ("43", TicketHoldCases.ImageUntrusted), ("41", TicketHoldCases.ModelWait));
        view.Holds[1].Should().Match<AdminHoldView>(h => h.Since == Now.AddMinutes(-90) && h.Title == "Ticket 41"
            && h.Hold.Escalation!.AfterMinutes == 60 && h.Hold.Escalation.Key == "holds.escalate_after_minutes");
        view.Holds[0].Hold.Escalation.Should().BeNull("a precondition stop is the admin's by what ended it");
    }

    [Fact]
    public async Task WaitingSince_ReadsOnlyTheOpenModelWaits()
    {
        await _h.AddAsync(Run("r-1", "41", status: RunStatuses.Running), Wait("r-1", Now.AddMinutes(-5)),
            Run("r-2", "42", status: RunStatuses.Running), new ModelRunWait { RunId = "r-2", WaitedMs = 1000 });
        await using var scope = _h.Services.CreateAsyncScope();

        var waits = await scope.ServiceProvider.GetRequiredService<ModelRunWaitClock>().WaitingSinceAsync(default);

        waits.Should().BeEquivalentTo(new Dictionary<string, DateTimeOffset> { ["r-1"] = Now.AddMinutes(-5) });
    }

    [Fact]
    public async Task EnqueuedAt_ReadsEachQueuedTicketsArrival()
    {
        await _h.AddAsync(Queue("41", "r-1", Now.AddMinutes(-3)));
        await using var scope = _h.Services.CreateAsyncScope();

        var arrivals = await scope.ServiceProvider.GetRequiredService<QueuedTicketRepository>().EnqueuedAtAsync(default);

        arrivals.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<(string, string), DateTimeOffset>(("alpha", "41"), Now.AddMinutes(-3)));
    }

    private Task<TicketListView> ListAsync() =>
        _h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), null, null, null), default);

    private static ModelRunWait Wait(string runId, DateTimeOffset since) => new() { RunId = runId, Key = "gpt", WaitingSince = since };

    private static QueuedTicket Queue(string ticket, string runId, DateTimeOffset at) => new()
    {
        Project = "alpha", TicketId = ticket, Pipeline = "code", Platform = "github", ReservedRunId = runId, Reason = "full",
        EnqueuedAt = at,
    };
}
