using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>
/// 2026-10-15-114fb: close marks, pull-request records and their entries on the store harness's GitHub project
/// "alpha" (tracker "gh"); every attempt the harness adds starts at T.
/// </summary>
internal static class ClosedFixtures
{
    public static Task CloseAsync(this TicketStoreHarness h, DateTimeOffset at, string ticket = "42",
        string project = "alpha", string? note = "duplicate of 41") =>
        h.Services.GetRequiredService<ITicketClosedMarkStore>().SetAsync(new TicketClosedMark(
            "gh", TicketKey.For("github", ticket).Value, ticket, project, at, "Ada", note, [], false), default);

    public static string Url(string number) => $"https://github.com/o/api/pull/{number}";

    public static PrReviewRecord Record(string number, string state, string ticket = "42", double stateHours = 1) => new()
    {
        PrKey = PrKey.Of(Url(number)), Url = Url(number), Repo = "api", Number = number, Project = "alpha", TicketId = ticket,
        RunId = "r-1", State = state, StateAtTicks = T.AddHours(stateHours).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
    };

    public static PrReviewEntry Entry(string number, string hostId, DateTimeOffset at,
        bool ours = false, bool bot = false, bool system = false, string? decision = null) => new()
    {
        PrKey = PrKey.Of(Url(number)), HostId = hostId, Kind = decision is null ? PrReviewEntryKinds.Comment : PrReviewEntryKinds.Review,
        AuthorLogin = ours ? "agent" : bot ? "ci[bot]" : "grace", IsOurs = ours, IsBot = bot, IsSystem = system,
        Decision = decision, Standing = decision is not null, Body = "note", AtTicks = at.UtcTicks,
    };

    public static async Task<TicketSummaryView> RowAsync(this TicketStoreHarness h, string ticket = "42") =>
        (await h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Tickets.Single(t => t.TicketId == ticket);
}
