using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-28-1da5c: ticket_work — what the TRACKER shows against this conversation's ticket.
/// <para>
/// Asked whether a ticket's branches were finished, a design turn could only answer that every
/// link demanded a sign-in: its one reach outward holds no credential. This holds one, and answers
/// a named question rather than fetching a URL — so nothing here can be talked into a write on a
/// surface that performs none.
/// </para>
/// </summary>
public sealed class TicketWorkToolHost(IBoundTicketWork work) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Work, name: "ticket_work")];
    }

    [Description(
        "What THIS conversation's ticket has against it on its tracker: the pull requests it is "
        + "linked to, with their state, and the branches where the tracker links those too. Use it "
        + "before saying whether work on the ticket is finished. It reports what the TRACKER shows "
        + "— not what this framework's own runs recorded — and a tracker that cannot be asked says "
        + "so with its reason rather than answering that there is nothing.")]
    private async Task<TicketLinkedWorkResult> Work(CancellationToken cancellationToken) =>
        await work.ForAsync(cancellationToken);
}
