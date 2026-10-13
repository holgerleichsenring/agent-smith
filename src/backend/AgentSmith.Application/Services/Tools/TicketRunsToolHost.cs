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
        "What THIS FRAMEWORK did about this conversation's ticket: the runs it made, the specs "
        + "they worked and the pull requests they opened. Each run carries why it started (cause "
        + "channel ticket_comment, pull_request, status_move, requested, retrigger, pickup, operator "
        + "or retry, with who and when); each spec its status, verdict, criteria outcomes (met, unmet, "
        + "not_applicable, unproven, with note and citation), declined criteria and unmeasured accounts "
        + "such as a red build. pullRequestReviews lists every pull request's review entries — kind "
        + "(comment, review, decision), decision, author, file and line: an entry with trusted false "
        + "started nothing, and a system entry is the host's event, not a person's. This is the framework's OWN record — a "
        + "branch pushed by hand or a pull request opened outside a run is not in it. Use "
        + "ticket_work for what the tracker shows, and say which of the two an answer rests on. "
        + "Approving a proposal here continues the ticket's series by spec id: repeating an id that has "
        + "not run re-cuts it, a spec that ran is kept as it ran, a new id is appended, and a lone spec "
        + "leaves the rest of the series kept while an epic drops what it leaves out.")]
    private async Task<TicketRunsResult> Runs(CancellationToken cancellationToken) =>
        await runs.ForAsync(cancellationToken);
}
