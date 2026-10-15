using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>
/// 2026-10-14-dbcen: filings, requires and landings on the store harness's GitHub project "alpha" (tracker "gh") —
/// a filing row is what 2026-10-14-dbcel's per-ticket record leaves behind.
/// </summary>
internal static class NeverRunFixtures
{
    public const string Tracker = "gh";

    public static string KeyOf(string id) => TicketKey.For("github", id).Value;

    public static ApprovedSeries Filing(
        string id, string? title = null, bool ticketText = true, string project = "alpha", DateTimeOffset? at = null)
    {
        var approval = new SpecApproval(at ?? T, "conversation-1", "sub-1");
        var set = new SpecSet(KeyOf(id), [], SpecAccounting.Empty, [], SpecSource.Approved, Approval: approval) { Goal = title };
        var record = new SpecApprovalRecord(KeyOf(id), set, ["api"], Tracker, "", id) { Project = project, TicketText = ticketText };
        return new ApprovedSeries
        {
            Tracker = Tracker, TicketKey = KeyOf(id), TicketId = id, Project = project, SeriesId = "",
            ContentJson = SpecApprovalJson.Write(record), ApprovedAt = at ?? T, ApprovedInConversation = "conversation-1",
        };
    }

    public static Task RequiresAsync(this TicketStoreHarness h, string id, params string[] required) =>
        h.Services.GetRequiredService<ITicketRequirementStore>().AddAsync(
            [.. required.Select(r => new TicketRequirementEdge(Tracker, KeyOf(id), id, "alpha", KeyOf(r), r, true, default, "conversation-1"))],
            default);

    public static Task LandAsync(this TicketStoreHarness h, string id, string kind = TicketLandingKinds.ByHand, DateTimeOffset? at = null) =>
        h.Services.GetRequiredService<ITicketLandingStore>().TryRecordAsync(new TicketLandingFact(
            Tracker, KeyOf(id), id, "alpha", kind, at ?? T.AddHours(2),
            kind == TicketLandingKinds.ByHand ? "Ada Lovelace" : null, kind == TicketLandingKinds.ByHand ? "shipped by hand" : null, null),
            default);

    public static async Task<TicketListView> ListAsync(this TicketStoreHarness h, string? bucket = null, string? cursor = null, int? limit = null) =>
        await h.List.ReadAsync(new TicketListRequest(ProjectScope.Single("alpha"), bucket, cursor, limit), default);
}
