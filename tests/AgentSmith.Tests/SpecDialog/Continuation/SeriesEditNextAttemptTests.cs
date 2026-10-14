using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-14-da5ee: the series edited in the conversation is the series the next attempt runs. The edit and the
/// start go through the ports a bound turn is seeded with; the record they leave is then resolved by the real
/// derivation handler over a branch whose head already ran.
/// </summary>
public sealed class SeriesEditNextAttemptTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private const string Key = "azdo-19106";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c", D = Series + "d";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task NextAttempt_AfterEditAndStart_RunsTheEditedSeries()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);
        var poster = new TurnPoster("ops-2", MayStartRuns: true);
        var project = _h.Config.Projects["alpha"];
        var series = await _h.InScopeAsync(sp => sp.GetRequiredService<BoundSeriesPorts>()
            .ForAsync(project, ContinuationHarness.TicketId, ContinuationHarness.SessionId, poster, default));
        var start = await _h.InScopeAsync(sp => sp.GetRequiredService<BoundAttemptStarts>()
            .ForAsync(project, ContinuationHarness.TicketId, ContinuationHarness.SessionId, poster, default));

        var edited = await series!.Edit!.EditAsync(
        [
            new SeriesEdit(SeriesEditKinds.Recut, B, SeriesEditTests.Spec(B, "B, re-cut")),
            new SeriesEdit(SeriesEditKinds.Drop, C),
            new SeriesEdit(SeriesEditKinds.Append, Yaml: SeriesEditTests.Spec("2026-10-14-0000", "New work")),
        ], default);
        var started = await start!.StartNextAsync("the series is edited, go on", default);

        edited.Should().Contain("nothing was started");
        started.Should().StartWith("Requested with the edited series");
        var record = (await _h.RecordAsync())!;
        record.Continuation!.StartsAttempt.Should().BeTrue("the start approved the pending edit");

        var next = NextAttempt();
        await next.Approvals.SaveAsync(record with { Key = Key, Tracker = ApprovedSets.Tracker, Set = record.Set with { Key = Key } }, default);
        await next.Handler().ExecuteAsync(next.Context(Ticket()), default);

        next.Writer.Written!.Specs.Select(s => (s.SpecId, s.Draft.Goal)).Should().Equal(
            (A, "Goal " + A), (B, "B, re-cut"), (D, "New work"));
        next.Writer.Written.Current.Cause.Should().StartWith(SpecRevisionCause.Approval);
        next.Writer.Written.Approval!.Principal.Should().Be("ops-2");
        next.Deriver.Calls.Should().Be(0, "an approved continuation is applied, never re-derived");
    }

    private static Ticket Ticket() => new(new TicketId("19106"), "A ticket", "Migrate the client.", null, "open", "azdo", []);

    // The ticket branch as the next attempt reads it: A ran, B and C are still planned.
    private static ApprovedSetHarness NextAttempt()
    {
        var harness = new ApprovedSetHarness { Branch = { Key = Key }, PointerSha = "branch-sha" };
        harness.Tracker = harness.Notices.Tracker;
        harness.Branch.SeedSeries(Series, string.Join("\n",
            $"ticket: {Key}", "specs:", $"- {A}", $"- {B}", $"- {C}", "revisions:", "- number: 1",
            "  cause: approved in design conversation session-77", "  at: 2026-09-17T12:00:00.0000000+00:00",
            $"ticket_fingerprint: {TicketTextFingerprint.Of(Ticket())}",
            "approved_at: 2026-09-17T12:00:00.0000000+00:00", "approved_in_conversation: session-77",
            "approved_by: sample.approver"),
            new Dictionary<string, string>
            {
                [$"{A}-first"] = Yaml(A), [$"{B}-second"] = Yaml(B), [$"{C}-third"] = Yaml(C),
            }, executed: A);
        return harness;
    }

    private static string Yaml(string id) => $"spec: {id}\ngoal: \"Goal {id}\"\ndone:\n  - \"Done {id}.\"";
}
