using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0356: the same-ticket RESUME seed — the latest prior run's persisted ledger
// becomes the next run's opening checklist, gated on progressed-past-bootstrap
// and an age cap. Cross-run context ingestion stays successful-runs-only; this
// gate is deliberately outcome-agnostic (a reaped run's ledger is the point).
public sealed class PriorRunLedgerSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private static PriorRunLedger Prior(
        TimeSpan age, params ProgressLedgerItemView[] items) =>
        new("2026-07-20T0800-abcd", Now - age, items);

    private static ProgressLedgerItemView Item(
        string id, string status, string? note = null, string? target = null) =>
        new(id, $"activity {id}", status, target, note);

    [Fact]
    public void Seed_LatestSameTicketRunWithProgress_CarriesStatusTargetAndNote()
    {
        var prior = Prior(TimeSpan.FromHours(2),
            Item("1", "done", note: "convention: use IFoo everywhere", target: "src/A.cs"),
            Item("2", "in_progress"),
            Item("3", "pending"));

        var seed = PriorRunLedgerSeeder.Seed(prior, Now);

        seed.Should().HaveCount(3);
        seed[0].Status.Should().Be(ProgressStatus.Done);
        seed[0].Note.Should().Be("convention: use IFoo everywhere");
        seed[0].Target.Should().Be("src/A.cs");
        seed[1].Status.Should().Be(
            ProgressStatus.Pending, "the interrupted in_progress step is re-verified, not assumed done");
        seed[2].Status.Should().Be(ProgressStatus.Pending);
    }

    [Fact]
    public void Seed_BootstrapAbortedPriorRun_AllPending_NotIngested()
    {
        // The mid-run flush persists ledgers EARLY, so a run reaped during
        // bootstrap leaves an all-pending ledger behind — nothing validated it.
        var prior = Prior(TimeSpan.FromHours(1), Item("1", "pending"), Item("2", "pending"));

        PriorRunLedgerSeeder.Seed(prior, Now).Should().BeEmpty();
    }

    [Fact]
    public void Seed_PriorRunOlderThanAgeCap_NotIngested()
    {
        var prior = Prior(PriorRunLedgerSeeder.MaxAge + TimeSpan.FromMinutes(1), Item("1", "done"));

        PriorRunLedgerSeeder.Seed(prior, Now).Should().BeEmpty();
    }

    [Fact]
    public void Seed_NoPriorLedger_Empty()
    {
        PriorRunLedgerSeeder.Seed(null, Now).Should().BeEmpty();
        PriorRunLedgerSeeder.Seed(Prior(TimeSpan.Zero), Now).Should().BeEmpty();
    }

    [Fact]
    public void Seed_OversizedPriorLedger_CappedAtMaxItems()
    {
        var items = Enumerable.Range(1, ProgressLedger.MaxItems + 5)
            .Select(i => Item(i.ToString(), i == 1 ? "done" : "pending"))
            .ToArray();

        var seed = PriorRunLedgerSeeder.Seed(Prior(TimeSpan.FromHours(1), items), Now);

        seed.Should().HaveCount(ProgressLedger.MaxItems);
    }

    [Fact]
    public async Task Seed_SeededEntries_RoundTripIntoToolHost()
    {
        // The resume seed round-trips into the tool host. p0359 let the resumed
        // model restructure freely; p0368 preserves COMPLETED prior work — a prior
        // DONE step survives a rewrite that omits it (pending prior work stays
        // droppable), so a resume never re-treads finished steps.
        var seed = PriorRunLedgerSeeder.Seed(
            Prior(TimeSpan.FromHours(1), Item("1", "done"), Item("2", "pending")), Now);
        var host = new ProgressLedgerToolHost(seed);

        var result = await host.UpdateProgress(new List<ProgressUpdateItem>
        {
            new("2", "activity 2", "done"),
        });

        result.Should().NotContain("Error");
        host.GetLedger().Entries.Select(e => e.Id).Should().BeEquivalentTo("1", "2");
    }
}
