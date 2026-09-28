using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-28-1da5d: ticket_runs — what THIS FRAMEWORK did about this conversation's ticket.
/// <para>
/// Deliberately a second tool rather than more fields on the tracker's answer. "The run says it is
/// done" and "the board says it is done" are not one claim, and a single answer carrying both
/// would be more confident than either source.
/// </para>
/// </summary>
public sealed class TicketRunsToolHost(IBoundTicketRuns runs) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Runs, name: "ticket_runs")];
    }

    [Description(
        "What THIS FRAMEWORK did about this conversation's ticket: the runs it made, the phases "
        + "they worked and the pull requests they opened. This is the framework's OWN record — a "
        + "branch pushed by hand or a pull request opened outside a run is not in it. Use "
        + "ticket_work for what the tracker shows, and say which of the two an answer rests on.")]
    private async Task<TicketRunsResult> Runs(CancellationToken cancellationToken) =>
        await runs.ForAsync(cancellationToken);
}
