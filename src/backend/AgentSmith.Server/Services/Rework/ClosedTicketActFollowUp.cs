using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59t: an act on a ticket a person closed files ONE linked follow-up ticket carrying the act's
/// text instead of reopening it. One per tracker ticket: under the tracker ticket's own lock the act's key is
/// marked on the ledger row of the first project (by name) sharing the tracker, FIRST — a key already marked
/// files nothing, so a redelivery or a second matching project stays silent, and a failed create is never
/// repeated. The spent key is the act's served mark. An overruled default (2026-10-13-8762p) is followed up the
/// same way, its notes the body; an operator approval or a retry is answered on the ticket instead.
/// </summary>
public sealed class ClosedTicketActFollowUp(
    IServiceScopeFactory scopes, IRedisClaimLock locks, ITicketProviderFactory tickets, ReworkSpeech speech,
    ILogger<ClosedTicketActFollowUp> logger)
{
    public static string KeyOf(ReworkAct act) => $"follow-up|{act.Channel}|{act.At.UtcTicks}|{act.Author}";

    public async Task<bool> IsFollowedUpAsync(ResolvedProject project, string ticketId, PendingReworkAct act, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        return await sp.GetRequiredService<ReworkLedgerRepository>()
            .IsSpokenAsync(RowOf(sp.GetRequiredService<FiledWorkTrackerProjects>(), project), ticketId, KeyOf(act.Act), ct);
    }

    public async Task<ReworkNudgeDisposition> AnswerAsync(
        ResolvedProject project, string ticketId, PendingReworkAct act, string runId, CancellationToken ct)
    {
        if (act.Act.Channel is not (ReworkChannel.Ticket or ReworkChannel.PullRequest or ReworkChannel.Overrule))
        {
            await speech.SayOnceAsync(project, ticketId, act, runId, "closed", ReworkTexts.TicketRefusal(ReworkTexts.ClosedRefusal, act.Act.Channel),
                ReworkTexts.PrRefused(ticketId, ReworkTexts.ClosedRefusal), ct);
            return ReworkNudgeDisposition.Finish;
        }
        var lockKey = $"agentsmith:rework-followup:{project.Tracker.Name}:{ticketId}";
        if (await locks.TryAcquireAsync(lockKey, ReworkNudgeRepository.ClaimLease, ct) is not { } token)
            return ReworkNudgeDisposition.Reschedule;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = FollowUpServices.Of(scope.ServiceProvider);
            if (await services.Ledger.TryMarkSpokenAsync(RowOf(services.Projects, project), ticketId, KeyOf(act.Act), ct))
                await FileAsync(services, project, ticketId, act, runId, ct);
            return ReworkNudgeDisposition.Finish;
        }
        finally
        {
            await locks.ReleaseAsync(lockKey, token, CancellationToken.None);
        }
    }

    private async Task FileAsync(
        FollowUpServices services, ResolvedProject project, string ticketId, PendingReworkAct act, string runId, CancellationToken ct)
    {
        try
        {
            var provider = tickets.Create(project.Tracker);
            var closed = await provider.GetTicketAsync(new TicketId(ticketId), ct);
            var title = TicketTitle.Fit($"Follow-up: {closed.Title}");
            var body = ReworkTexts.FollowUpBody(act.Text ?? act.PrUrl ?? string.Empty,
                FiledTicketKey.Of(project.Tracker.Type, ticketId), act.Act.Author);
            var created = await provider.CreateAsync(title, body, [], services.Kinds.For(project, TicketFilingRole.Work), ct);
            List<FiledTicket> filed = [FiledTicket.Of(created, title, project)];
            await services.Starter.StampAsync(provider, project, created, [], mayStartRuns: true, filed, ct);
            await services.Link.LinkAsync(project, ticketId, filed[0], ct, askedBy: act.Act.Author);
            if (act.Repo is not null)
                await speech.SayOnceAsync(project, ticketId, act, runId, "followed-up", string.Empty,
                    ReworkTexts.PrFollowedUp(ticketId, filed[0].Key ?? filed[0].Reference), ct);
            logger.LogInformation("Closed ticket {Project}/#{Ticket} followed up as {FollowUp}", project.Name, ticketId, created.Id.Value);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The follow-up of closed ticket {Project}/#{Ticket} was not filed; its act stays spent",
                project.Name, ticketId);
        }
    }

    private static string RowOf(FiledWorkTrackerProjects projects, ResolvedProject project) =>
        projects.SharingTrackerWith(project.Name).Select(p => p.Name).Order(StringComparer.Ordinal).FirstOrDefault() ?? project.Name;
}

/// <summary>2026-10-13-9a59t: what one follow-up filing resolves inside its one scope.</summary>
internal sealed record FollowUpServices(
    ReworkLedgerRepository Ledger, FiledWorkTrackerProjects Projects, TicketKindResolver Kinds, FiledWorkStarter Starter, FollowUpLink Link)
{
    internal static FollowUpServices Of(IServiceProvider sp) => new(
        sp.GetRequiredService<ReworkLedgerRepository>(), sp.GetRequiredService<FiledWorkTrackerProjects>(),
        sp.GetRequiredService<TicketKindResolver>(), sp.GetRequiredService<FiledWorkStarter>(), sp.GetRequiredService<FollowUpLink>());
}
