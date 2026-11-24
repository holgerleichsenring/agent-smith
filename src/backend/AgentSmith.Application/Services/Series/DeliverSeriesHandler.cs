using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-24-f9c5e: a beat's delivery of a series with a series branch. Once every ticket landed, each repo a ticket pull request
/// merged into the series branch gets one pull request from it into the default branch (<see cref="SeriesFinalPullRequest"/>);
/// in Delivering — the series check passed with no new failure — they merge under merge.when, and the series is done once every
/// one merged, which also wakes the tickets outside the series that waited for it. A series without a branch has nothing here.
/// </summary>
public sealed class DeliverSeriesHandler(ISeriesRunStore store, SeriesFinalPullRequest finals, ISeriesTicketActs acts, IServiceScopeFactory scopes)
    : ICommandHandler<DeliverSeriesContext>
{
    public async Task<CommandResult> ExecuteAsync(DeliverSeriesContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SeriesBeat>(ContextKeys.SeriesBeat, out var beat) || beat is null) return CommandResult.Ok("No series to deliver.");
        if (beat.Record.IntegrationBranch is not { } branch) return CommandResult.Ok("The series has no series branch; nothing to deliver.");
        if (beat.Facts.Count == 0 || !beat.Facts.All(f => f.Landed)) return CommandResult.Ok($"Not every ticket landed on {branch} yet.");
        var merging = beat.Record.State == SeriesRunStates.Delivering;
        var (repos, unread) = await ReposAsync(context.Project, beat.Record, branch, cancellationToken);
        if (unread) return CommandResult.Ok("A landed ticket's pull request has no target read yet; the next beat delivers.");
        var steps = new List<(string Repo, SeriesDeliveryStep Step)>();
        foreach (var repo in repos) steps.Add((repo.Name, await StepAsync(context, beat, repo, branch, merging, cancellationToken)));
        beat.Said.AddRange(steps.Select(s => s.Step.Say).OfType<string>());
        var done = merging && steps.Count > 0 && steps.All(s => s.Step.Merged);
        if (await store.UpdateAsync(beat.Record.Id, r => r with
            {
                Deliveries = [.. steps.Select(s => s.Step.Delivery).OfType<SeriesDelivery>()], LastDeliveredAt = beat.Now,
                State = done && r.State == SeriesRunStates.Delivering ? SeriesRunStates.Done : r.State,
                FinishedAt = done && r.State == SeriesRunStates.Delivering ? beat.Now : r.FinishedAt,
            }, cancellationToken) is { } kept) beat.Record = kept;
        if (done) await EndAsync(context, beat, branch, cancellationToken);
        return CommandResult.Ok(steps.Count == 0 ? "No ticket pull request merged into the series branch; nothing to deliver."
            : string.Join("; ", steps.Select(s => $"{s.Repo}: {s.Step.Line}")));
    }

    // One repo's step; a host that fails is said and tried again next beat, and never stops the beat's other steps.
    private async Task<SeriesDeliveryStep> StepAsync(DeliverSeriesContext context, SeriesBeat beat, RepoConnection repo, string branch,
        bool merging, CancellationToken ct)
    {
        var known = beat.Record.Deliveries.FirstOrDefault(d => d.Repo == repo.Name);
        try { return await finals.StepAsync(context, beat.Record, repo, branch, known, merging, beat.Now, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return SeriesDeliveryStep.Of(known ?? new SeriesDelivery(repo.Name, string.Empty, string.Empty), "failed",
                $"The final pull request of {repo.Name} could not be read or opened: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
    }

    private async Task EndAsync(DeliverSeriesContext context, SeriesBeat beat, string branch, CancellationToken ct)
    {
        var woken = await acts.WakeDependentsAsync(context.Project.Name, [.. beat.Record.Tickets.Select(t => t.TicketId)], ct);
        beat.Said.Add($"The series reached the default branch from {branch} in every repository; it is done."
            + (woken > 0 ? $" {woken} ticket(s) outside it that waited for it can start now." : string.Empty));
    }

    // The project's repos a ticket pull request of the series merged into the series branch, in the project's order.
    // Unread: a merged ticket pull request's target is not read yet, so where the series has work is not known yet either.
    private async Task<(IReadOnlyList<RepoConnection> Repos, bool Unread)> ReposAsync(ResolvedProject project, SeriesRunRecord record,
        string branch, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (scope.ServiceProvider.GetService<IPrReviewRecordReader>() is not { } reader) return ([], true);
        var repos = new HashSet<string>(StringComparer.Ordinal);
        var unread = false;
        foreach (var ticket in record.Tickets)
            foreach (var pr in (await reader.ByTicketAsync(project.Name, ticket.TicketId, ct)).Where(r => r.State == PrReviewStates.Merged))
                if (pr.TargetBranch == branch) repos.Add(pr.Repo);
                else unread |= pr.TargetBranch is null;
        return ([.. project.Repos.Where(r => repos.Contains(r.Name))], unread);
    }
}
