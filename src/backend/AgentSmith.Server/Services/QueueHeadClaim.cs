using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// The claim request the capacity-queue pump makes for its head, from the entry's stored context and plan answers. 2026-11-27-3532a:
/// at the claim it carries the approved set read NOW (<see cref="ApprovedSpecSetCarrier"/>), so a set approved while the ticket
/// waited — an add_ticket spec, a revision — reaches the run; when no record is read, the stored context stays as it was.
/// </summary>
public sealed class QueueHeadClaim(ApprovedSpecSetCarrier? sets = null) : AgentSmith.Server.Contracts.IQueueHeadClaim
{
    /// <summary>The request the entry stored, as the immediate claim would have made it.</summary>
    public static ClaimRequest Stored(CapacityQueueEntry head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return new(head.Platform, head.Project, new TicketId(head.TicketId), head.Pipeline,
            InitialContext: Deserialize<Dictionary<string, object>>(head.InitialContextJson),
            PlanAnswers: Deserialize<Dictionary<string, string>>(head.PlanAnswersJson),
            ExistingRunId: head.ReservedRunId);
    }

    /// <summary>The stored request with the approved set as the store holds it at this claim.</summary>
    public async Task<ClaimRequest> AtClaimAsync(CapacityQueueEntry head, ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var request = Stored(head);
        if (sets is null || await sets.JsonForAsync(project.Tracker.Name, head.Platform, head.TicketId, ct) is not { } json) return request;
        return request with { InitialContext = new(request.InitialContext ?? []) { [ContextKeys.ApprovedSpecSet] = json } };
    }

    public static bool IsRequestedByName(CapacityQueueEntry head) =>
        Deserialize<Dictionary<string, object>>(head?.InitialContextJson).IsRequestedByName();

    // Same JSON round-trip as the Redis job queue, so context values reach the pipeline alike.
    private static T? Deserialize<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(json);
}
