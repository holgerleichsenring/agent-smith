using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Posts a Plan open-questions comment to the originating ticket. Resolves
/// the platform-specific <see cref="Contracts.Tickets.ITicketCommentTemplate"/>
/// via DI (keyed singleton on TicketConfig.Type) and uses the existing
/// <see cref="Contracts.Providers.ITicketProvider.UpdateStatusAsync"/> seam to
/// deliver the comment.
/// </summary>
public interface IPlanOpenQuestionsPoster
{
    Task PostAsync(
        TicketConfig ticketConfig, TicketId ticketId,
        IReadOnlyList<PlanOpenQuestion> questions, CancellationToken cancellationToken);
}
