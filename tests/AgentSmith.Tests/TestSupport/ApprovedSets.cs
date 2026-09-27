using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: the shapes the approved-set cases build — a record, its set and its phases —
/// so each test states only what it is about.
/// </summary>
internal static class ApprovedSets
{
    internal static readonly DateTimeOffset Noon =
        new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The tracker CONNECTION these records live on — the other half of their identity.</summary>
    internal const string Tracker = "sample-tracker";

    internal static SpecPhase Phase(string id, string goal = "Do the thing") => new(
        new PhaseDraft(id, goal, $"phase: {id}\ngoal: \"{goal}\"", []) { Done = [$"Done {id}."] },
        id, string.Empty, []);

    internal static SpecSet Set(
        string key, IReadOnlyList<SpecPhase> phases, SpecApproval? approval,
        SpecSource source = SpecSource.Approved) =>
        new(key, phases, SpecAccounting.Empty, [], source, Approval: approval);

    internal static SpecApproval Approval(DateTimeOffset at, string conversation = "session-1") =>
        new(at, conversation, "sample.approver");

    /// <param name="ticketId">2026-09-25-c1f7: the tracker's OWN ticket id. Empty by default,
    /// which is a record no discovery query can name — what every record written before that
    /// phase looks like.</param>
    internal static SpecApprovalRecord Record(
        string key, DateTimeOffset at, IReadOnlyList<string>? phaseIds = null,
        IReadOnlyList<string>? repositories = null, string conversation = "session-1",
        string tracker = Tracker, string ticketId = "") =>
        new(key,
            Set(key, [.. (phaseIds ?? ["p0001a"]).Select(id => Phase(id))], Approval(at, conversation)),
            repositories ?? [],
            tracker,
            string.Empty,
            ticketId);
}
