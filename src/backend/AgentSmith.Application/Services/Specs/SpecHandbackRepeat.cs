using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Decides whether a hand-back repeats the previous one with nothing new said. A signal
/// that fires forever teaches the operator to ignore it, so the second contradiction in
/// a row with no reply on the ticket ends the loop and the run continues.
/// <para>
/// 2026-09-07-bd7a: the conversation is the signal — the same one the question case
/// reads. The pointer's sha comparison it replaces was unreachable: every derivation
/// commits a fresh timestamped revision, so the sha recorded at the last park never
/// equalled the head at the next, and a contradiction parked again on every re-trigger.
/// </para>
/// <para>
/// Only the contradiction case can end its loop. A REFUSAL parks every time — continuing
/// past what must not be done is the one wrong answer. A QUESTION is ended by the pin: an
/// unanswered one is answered into the next derivation, so a second park is the model
/// asking again. The NOT-IMPLEMENTABLE verdict restarts only on a Retry that clears the
/// pointer's case, so it is never a repeat by construction.
/// </para>
/// </summary>
public sealed class SpecHandbackRepeat(ILogger<SpecHandbackRepeat> logger)
{
    /// <summary>True when this hand-back repeats the previous one and nobody replied since.</summary>
    public bool IsRepeat(SpecSetPointer? pointer, SpecHandbackCase current, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (current is not SpecHandbackCase.RequirementsContradictRepository) return false;
        if (pointer is null || pointer.LastHandbackCase != current) return false;
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        if (OwnTicketComment.IsAnswered(comments, SpecHandbackComment.ContradictionMarker))
        {
            logger.LogInformation(
                "Spec {Key}: a person replied since the last '{Case}' hand-back — parking again",
                pointer.Key, current);
            return false;
        }
        return true;
    }
}
