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
    /// <remarks>
    /// p0454: takes the TICKET, not just its id — the comment waits for an answer, so it
    /// names the person it waits for.
    /// p0461: and the PIPELINE, because the comment also has to say where answering
    /// resumes the run, and only the pipeline knows which run this is.
    /// 2026-09-22-7c41b: it ANSWERS what the tracker did with the status. The status move is
    /// what makes the park persistent, so a caller that cannot see it refused has no way to
    /// know the ticket is still claimable. The two branches that move nothing answer
    /// <see cref="Contracts.Models.TicketFinalizeOutcome.NoStatusRequested"/> rather than a
    /// default, which would read as a successful move.
    /// </remarks>
    Task<Contracts.Models.TicketFinalizeResult> PostAsync(
        Contracts.Commands.PipelineContext pipeline, TrackerConnection ticketConfig, Ticket ticket,
        IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken cancellationToken);
}
