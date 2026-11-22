using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-22-6d85b: whether a ticket's merged pull requests reached the default branch. A stacked pull request targets the
/// ticket branch of a prerequisite it requires; merged there, its work reaches the default branch only with that
/// prerequisite's own merge in the same repository — and only when it merged before it (merged after, it sits on a branch
/// that already landed). A ticket that requires nothing, or a target that is no required ticket's branch (the default
/// branch, an epic's feature branch), lands as before. For a ticket that requires others, a merged pull request whose
/// target could not be read yet holds the landing until the next re-read reads it.
/// </summary>
public sealed class StackedMergeLanding(ITicketRequirementStore requirements, ITicketLandingStore landings, IServiceScopeFactory scopes)
{
    /// <summary>Why the merged <paramref name="records"/> have not reached the default branch yet; null when they have.</summary>
    public async Task<string?> UnreachedAsync(
        ResolvedProject project, string ticketId, IReadOnlyList<PrReviewRecordView> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var merged = records.Where(r => r.State == PrReviewStates.Merged).ToList();
        var tracker = project.Tracker.Name;
        var required = (await requirements.RequiredOfAsync(tracker, TicketLandingRecorder.KeyOf(project, ticketId), ct))
            .DistinctBy(e => e.RequiredTicketId, StringComparer.Ordinal)
            .ToDictionary(e => e.RequiredTicketId, e => e.RequiredTicketKey, StringComparer.Ordinal);
        if (required.Count == 0) return null;
        if (merged.FirstOrDefault(r => r.TargetBranch is null) is { } unread)
            return $"the branch {unread.Url} merged into is not read yet; the next re-read reads it";
        var stacked = merged.Where(r => TicketBranchNamer.TicketOf(r.TargetBranch) is { } on && on != ticketId && required.ContainsKey(on)).ToList();
        if (stacked.Count == 0) return null;
        var landed = (await landings.LandedAsync(tracker, [.. stacked.Select(r => required[TicketBranchNamer.TicketOf(r.TargetBranch)!])], ct))
            .ToDictionary(l => l.TicketKey, l => l.LandedAt, StringComparer.Ordinal);
        foreach (var record in stacked)
            if (await WhyAsync(project.Name, record, required, landed, ct) is { } why) return why;
        return null;
    }

    private async Task<string?> WhyAsync(string project, PrReviewRecordView record, IReadOnlyDictionary<string, string> required,
        IReadOnlyDictionary<string, DateTimeOffset> landed, CancellationToken ct)
    {
        var prerequisite = TicketBranchNamer.TicketOf(record.TargetBranch)!;
        if (!landed.TryGetValue(required[prerequisite], out var landedAt))
            return $"{record.Url} merged into {record.TargetBranch}; it reaches the default branch when #{prerequisite} lands";
        var cutoff = await MergedInRepoAsync(project, prerequisite, record.Repo, ct) ?? landedAt;
        return record.StateAt is { } at && at > cutoff
            ? $"{record.Url} merged into {record.TargetBranch} after #{prerequisite} merged; its work is not on the default branch"
            : null;
    }

    // When the prerequisite's own pull request in that repository merged; null when it has none there (a by-hand landing).
    private async Task<DateTimeOffset?> MergedInRepoAsync(string project, string prerequisite, string repo, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>().ByTicketAsync(project, prerequisite, ct);
        return records.Where(r => r.State == PrReviewStates.Merged && r.Repo == repo).Max(r => r.StateAt);
    }
}
