using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Tickets.FollowUps;

/// <summary>
/// 2026-11-18-c8e2d: a person files a follow-up a spec's master proposed. The follow-up is read from the run's stored
/// report for that spec, marked on the rework ledger so a second request files nothing, and filed through the 'file'
/// run every follow-up takes (2026-11-17-e061d) — one work ticket whose own run derives its specs.
/// </summary>
public sealed class TicketFollowUpAct(TicketProjects projects, IServiceScopeFactory scopes, IFilingRun filing)
{
    public async Task<TicketCloseResult> FileAsync(
        string project, string ticketId, TicketFollowUpRequest request, string by, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (projects.Of(project) is null) return TicketCloseResult.Refuse(StatusCodes.Status404NotFound, $"No project {project} is configured.");
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        if (await ProposedAsync(sp.GetRequiredService<IUnitOfWork>(), project, ticketId, request, ct) is not { } followUp)
            return TicketCloseResult.Refuse(StatusCodes.Status404NotFound,
                $"Run {request.RunId} of ticket {ticketId} proposed no follow-up {request.Index + 1} for spec {request.SpecId}.");
        var key = $"follow-up|{request.RunId}|{request.SpecId}|{request.Index}";
        if (!await sp.GetRequiredService<ReworkLedgerRepository>().TryMarkSpokenAsync(project, ticketId, key, ct))
            return TicketCloseResult.Refuse(StatusCodes.Status409Conflict, "This follow-up was already filed.");
        var report = await filing.RunRequestAsync(project, Request(followUp, ticketId, request, by));
        return report.Filed is [var filed, ..]
            ? TicketCloseResult.Ok(new { ticketId = filed.TicketId, key = filed.Key ?? filed.Reference, url = filed.Reference })
            : TicketCloseResult.Refuse(StatusCodes.Status502BadGateway, $"The follow-up was not filed: {report.Error}");
    }

    private static async Task<FollowUpView?> ProposedAsync(
        IUnitOfWork uow, string project, string ticketId, TicketFollowUpRequest request, CancellationToken ct)
    {
        var run = await uow.Set<Run>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RunId, ct);
        if (run is null || run.Project != project || run.TicketId != ticketId) return null;
        var spec = await uow.Set<RunSpec>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == request.RunId && r.SpecId == request.SpecId, ct);
        var followUps = RunStoryJson.TryDeserialize<AcceptanceView>(spec?.AcceptanceJson)?.FollowUps ?? [];
        return request.Index >= 0 && request.Index < followUps.Count ? followUps[request.Index] : null;
    }

    private static FilingRequest Request(FollowUpView followUp, string ticketId, TicketFollowUpRequest request, string by) =>
        new(string.Empty, by, [], [], Proposal: null, MayStartRuns: false)
        {
            FollowUp = new FilingFollowUp(followUp.Goal,
                $"{followUp.Why}\n\nProposed by the master of spec {request.SpecId} in run {request.RunId} of ticket {ticketId}, "
                + "as work it judged not worth holding that spec for."),
        };
}
