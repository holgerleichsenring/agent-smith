using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: the shapes the approved-set cases build — a record, its set and its specs —
/// so each test states only what it is about.
/// </summary>
internal static class ApprovedSets
{
    internal static readonly DateTimeOffset Noon =
        new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The tracker CONNECTION these records live on — the other half of their identity.</summary>
    internal const string Tracker = "sample-tracker";

    internal static DerivedSpec Spec(string id, string goal = "Do the thing") => new(
        new SpecDraft(id, goal, $"spec: {id}\ngoal: \"{goal}\"", []) { Done = [$"Done {id}."] },
        id, string.Empty, []);

    internal static SpecSet Set(
        string key, IReadOnlyList<DerivedSpec> specs, SpecApproval? approval,
        SpecSource source = SpecSource.Approved) =>
        new(key, specs, SpecAccounting.Empty, [], source, Approval: approval);

    internal static SpecApproval Approval(DateTimeOffset at, string conversation = "session-1") =>
        new(at, conversation, "sample.approver");

    /// <param name="ticketId">2026-09-25-c1f7: the tracker's OWN ticket id. Empty by default,
    /// which is a record no discovery query can name — what every record written before that
    /// spec looks like.</param>
    internal static SpecApprovalRecord Record(
        string key, DateTimeOffset at, IReadOnlyList<string>? specIds = null,
        IReadOnlyList<string>? repositories = null, string conversation = "session-1",
        string tracker = Tracker, string ticketId = "") =>
        new(key,
            Set(key, [.. (specIds ?? ["p0001a"]).Select(id => Spec(id))], Approval(at, conversation)),
            repositories ?? [],
            tracker,
            string.Empty,
            ticketId);
}
