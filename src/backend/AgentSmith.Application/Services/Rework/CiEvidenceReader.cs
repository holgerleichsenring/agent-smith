using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-11-26-49d3a: a run's read of the red CI on its ticket, beside the review read in FetchTicket — each open pull
/// request red on its head, with the failed checks its record stored, into <see cref="ContextKeys.CiFailures"/> for the
/// master's prompt. It sets no act and hands nothing back: the series master decides what a red CI means, the run starts
/// with the evidence. Recovered from 2026-11-17-3435c's CiRework without its loop.
/// </summary>
public sealed class CiEvidenceReader(IServiceScopeFactory scopes)
{
    public async Task ApplyAsync(FetchTicketContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TicketId is not { } ticket
            || !context.Pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) || project is null) return;
        IReadOnlyList<PrReviewRecordView> records;
        using (var scope = scopes.CreateScope())
            records = scope.ServiceProvider.GetService<IPrReviewRecordReader>() is { } reader
                ? await reader.ByTicketAsync(project.Name, ticket.Value, ct) : [];
        var red = Red(records);
        if (red.Count > 0) context.Pipeline.Set(ContextKeys.CiFailures, red);
    }

    /// <summary>The open records red on their current head, with their stored failures.</summary>
    public static IReadOnlyList<CiFailureFeedback> Red(IEnumerable<PrReviewRecordView> records) =>
        [.. records.Where(r => r.State == PrReviewStates.Open)
            .Select(r => (Record: r, Ci: PullRequestHeadCi.OnHead(r)))
            .Where(x => x.Ci is { State: CiStates.Red, Failures.Count: > 0 })
            .Select(x => new CiFailureFeedback(x.Record.Repo, x.Record.Url, x.Ci!.Failures))];
}
