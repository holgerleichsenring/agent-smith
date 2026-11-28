using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-28-db88a: a run's spec set is the one its TicketSeries row stores — "one path, truth in the DB" (operator). The
/// set is answered with the row's shas, so the next revision reads it as this system's own, and with the row's executed
/// ids; a series repository the run did not check out is answered as such; a ticket with no row has nothing yet.
/// <para>
/// A row that stores NO set is a ticket in flight at the deploy that started storing sets: its set is imported ONCE from
/// its branch by the old reader (<see cref="SeriesReader"/>), stored only if the row still has none, and from then on
/// only the row is read — a later edit of its manifest changes nothing. A branch read that did not answer cleanly (nothing,
/// unreadable, out of step) is answered as it always was and stores nothing. Follow-up 2026-11-28-5e0b deletes the import.
/// </para>
/// </summary>
public sealed class StoredSeriesReader(
    ISpecSetPointerStore store,
    SeriesReader legacyImport,
    SandboxTargets sandboxTargets,
    ILogger<StoredSeriesReader> logger) : ISpecSetReader
{
    public async Task<SpecSetOnBranch> ReadAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
        TicketKey ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var project = pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) && name is not null ? name : string.Empty;
        var pointer = await store.GetAsync(project, ticket.Value, cancellationToken);
        SpecSet? set;
        try { set = await store.GetSetAsync(project, ticket.Value, cancellationToken); }
        catch (InvalidDataException ex) { return SpecSetOnBranch.Unreadable(ex.Message); }
        if (set is null && pointer is null) return SpecSetOnBranch.Nothing;
        if (set is not null) return Checked(pipeline, set, repositories, StoredSeries.Read(set, pointer));

        var imported = await legacyImport.ReadAsync(pipeline, carryingRepo, repositories, ticket, cancellationToken);
        if (imported.Read is not { Lagging.Count: 0 } clean) return imported;
        await store.SaveSetAsync(project, clean.Set, cancellationToken, onlyIfAbsent: true);
        logger.LogInformation("Spec set {Key}: a row with no stored set, imported once from its branch", ticket.Value);
        var kept = await store.GetSetAsync(project, ticket.Value, cancellationToken) ?? clean.Set;
        // The import run keeps the shas it read off the branch, so an edit made there before the deploy is still named
        // as the revision's cause — once; every later read carries the row's own.
        return Checked(pipeline, kept, repositories, clean with { Set = StoredSeries.Read(kept, pointer).Set });
    }

    private SpecSetOnBranch Checked(
        PipelineContext pipeline, SpecSet set, IReadOnlyList<RepoConnection> repositories, SpecSetReadResult read) =>
        NotCheckedOut(pipeline, set, repositories) is { } member
            ? SpecSetOnBranch.NotCheckedOut($"series member '{member}' is not checked out in this run")
            : SpecSetOnBranch.Answered(read);

    private string? NotCheckedOut(PipelineContext pipeline, SpecSet set, IReadOnlyList<RepoConnection> repositories) =>
        (set.SeriesRepos ?? []).FirstOrDefault(member => sandboxTargets.SandboxesForRepo(pipeline,
            repositories.FirstOrDefault(r => string.Equals(r.Name, member, StringComparison.Ordinal))
            ?? new RepoConnection { Name = member }).Count == 0);
}

/// <summary>2026-11-28-db88a: a stored set as a read answers it — the run's own revision (the row's shas), its executed
/// ids the row's when it names any; one composition for the run and the host readers.</summary>
public static class StoredSeries
{
    public static SpecSetReadResult Read(SpecSet set, SpecSetPointer? pointer)
    {
        ArgumentNullException.ThrowIfNull(set);
        var executed = pointer?.ExecutedIdsOf(set.Specs) is { Count: > 0 } ids ? ids : set.Executed;
        var shas = (pointer?.ShaByRepo ?? new Dictionary<string, string>())
            .Where(kv => kv.Key.Length > 0).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        return new SpecSetReadResult(set with { Source = SpecSource.BranchArtifact, Executed = [.. executed] },
            pointer?.RevisionSha ?? string.Empty) { RevisionShas = shas };
    }
}
