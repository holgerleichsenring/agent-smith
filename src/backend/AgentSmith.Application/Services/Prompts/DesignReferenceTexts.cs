using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-01-7f7ad: the texts a master's Figma links are looked for in — a run's ticket
/// description, acceptance criteria and comment thread; a design turn's bound ticket and its
/// transcript. Ticket first, so the ten-link cap keeps what the request itself names.
/// </summary>
public static class DesignReferenceTexts
{
    public static IEnumerable<string?> From(PipelineContext pipeline, Ticket? ticket)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (ticket is not null)
        {
            yield return ticket.Description;
            yield return ticket.AcceptanceCriteria;
        }
        if (pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var comments) && comments is not null)
            foreach (var comment in comments)
                yield return comment.Body;
        if (pipeline.TryGet<SeededTicket>(ContextKeys.SpecDialogTicket, out var bound) && bound is not null)
            yield return bound.Text;
        if (pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, out var turns) && turns is not null)
            foreach (var turn in turns)
                yield return turn.Text;
    }
}
