using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-0781: the worker's voice. A text is said at most once per key — (run, act, kind),
/// kept in the ticket's ledger — and on the act's own channel: the pull request a review stands on,
/// otherwise the ticket. A post that fails is logged; the key stays spent, so nothing repeats.
/// </summary>
public sealed class ReworkSpeech(
    IServiceScopeFactory scopes, ITicketProviderFactory tickets, ISourceProviderFactory sources, ILogger<ReworkSpeech> logger)
{
    public async Task SayOnceAsync(
        ResolvedProject project, string ticketId, PendingReworkAct act, string runId, string kind,
        string ticketText, string prText, CancellationToken ct)
    {
        var key = $"{runId}|{act.Act.At.UtcTicks}|{kind}";
        if (!await MarkAsync(project.Name, ticketId, key, ct)) return;
        try
        {
            if (act.Repo is not null && PullRequestNumber.FromUrl(act.PrUrl) is { } pr
                && sources.Create(act.Repo) is IPrCommentProvider comments)
                await comments.PostCommentAsync(pr, prText, ct);
            else
                await tickets.Create(project.Tracker).UpdateStatusAsync(new TicketId(ticketId), ticketText, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not say '{Kind}' on {Project}/#{Ticket}", kind, project.Name, ticketId);
        }
    }

    private async Task<bool> MarkAsync(string project, string ticketId, string key, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>().TryMarkSpokenAsync(project, ticketId, key, ct);
    }
}
