using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Posts a Plan open-questions comment to the originating ticket. Resolves
/// the platform-specific <see cref="Contracts.Tickets.ITicketCommentTemplate"/>
/// via DI (keyed singleton on TrackerConnection.Type) and uses the existing
/// <see cref="Contracts.Providers.ITicketProvider.UpdateStatusAsync"/> seam to
/// deliver the comment.
/// </summary>
public interface IPlanOpenQuestionsPoster
{
    /// <summary>
    /// Posts the rendered open-questions comment. When <paramref name="parkStatus"/> is
    /// set, the comment and a native-status move to that status land in ONE provider call
    /// (FinalizeAsync) so the ticket is parked out of discovery until a human moves it back
    /// to a work status. When null, only the comment is posted (the ticket stays claimable).
    /// </summary>
    Task PostAsync(
        TrackerConnection ticketConfig, TicketId ticketId,
        IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken cancellationToken);
}
