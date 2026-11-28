using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-a0b6a: a ticket's spec set as its branch holds it, read on the host with no clone — what
/// every reader outside a run shows as the specs. The repositories are the ones the ticket's pointer
/// names (filing and every publish record it); each is read on the ticket branch through
/// <see cref="BranchFiles"/> by the same <see cref="SubSeriesFiles"/> a run's checkout is read by, and
/// joined by the same <see cref="SeriesComposer"/>. A ticket without a pointer has nothing on a branch
/// this system wrote. The revision sha per repository is the pointer's: the host is not asked for history.
/// <para>
/// 2026-11-28-db88a: the set is the one the ticket's row stores, so no branch is read — the series check, the final report
/// and every page work alike after cleanup deleted the branch. A row that stores no set (a ticket in flight at the deploy
/// that started storing sets) is imported ONCE by the branch read below, stored only if the row still has none, and read
/// from the row thereafter. Follow-up 2026-11-28-5e0b deletes the import.
/// </para>
/// </summary>
public sealed class TicketBranchSeries(
    ISpecSetPointerStore pointers,
    ISourceProviderFactory sources,
    SubSeriesFiles subSeries,
    SeriesComposer composer,
    ILogger<TicketBranchSeries> logger)
{
    public Task<SpecSetOnBranch> ReadAsync(
        ResolvedProject project, TicketKey ticket, TicketId ticketId, CancellationToken cancellationToken) =>
        ReadOnAsync(project, ticket, ticketId, landedOn: null, cancellationToken);

    /// <summary>
    /// 2026-11-27-ecf2d: the set of a ticket that landed. Cleanup deletes a merged work branch, so each repository is read
    /// on the ticket branch while that branch still reads, else where the ticket landed — <paramref name="seriesBranch"/>
    /// when the series has one, else the repository's default branch (configured, else the host's). A repository whose
    /// branch carried only the spec record kept its branch and got no pull request, so it is read there.
    /// </summary>
    public Task<SpecSetOnBranch> ReadLandedAsync(
        ResolvedProject project, TicketKey ticket, TicketId ticketId, string? seriesBranch, CancellationToken cancellationToken) =>
        ReadOnAsync(project, ticket, ticketId, new Landed(seriesBranch), cancellationToken);

    private sealed record Landed(string? SeriesBranch);

    private async Task<SpecSetOnBranch> ReadOnAsync(
        ResolvedProject project, TicketKey ticket, TicketId ticketId, Landed? landedOn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var pointer = await pointers.GetAsync(project.Name, ticket.Value, cancellationToken);
        SpecSet? stored;
        try { stored = await pointers.GetSetAsync(project.Name, ticket.Value, cancellationToken); }
        catch (InvalidDataException ex) { return SpecSetOnBranch.Unreadable(ex.Message); }
        if (stored is not null) return SpecSetOnBranch.Answered(StoredSeries.Read(stored, pointer));
        if (pointer is null) return SpecSetOnBranch.Nothing;
        var imported = await ImportAsync(project, ticket, ticketId, pointer, landedOn, cancellationToken);
        if (imported.Read is not { Lagging.Count: 0 } clean) return imported;
        await pointers.SaveSetAsync(project.Name, clean.Set, cancellationToken, onlyIfAbsent: true);
        logger.LogInformation("Spec set {Key}: a row with no stored set, imported once from its branch", ticket.Value);
        return SpecSetOnBranch.Answered(StoredSeries.Read(
            await pointers.GetSetAsync(project.Name, ticket.Value, cancellationToken) ?? clean.Set, pointer));
    }

    // The legacy branch read, for a row in flight only.
    private async Task<SpecSetOnBranch> ImportAsync(
        ResolvedProject project, TicketKey ticket, TicketId ticketId, SpecSetPointer pointer, Landed? landedOn,
        CancellationToken cancellationToken)
    {
        var branch = TicketBranchNamer.Compose(ticketId);
        // 2026-11-23-1e76a: the repositories side by side, in the pointer's order.
        var reads = await Task.WhenAll(RepoNames(pointer).Select(name =>
            ReadAsync(project, name, pointer, ticket, branch, landedOn, cancellationToken)));
        return composer.Compose(ticket, pointer.CarryingRepo, reads);
    }

    private async Task<SubSeriesRead> ReadAsync(
        ResolvedProject project, string name, SpecSetPointer pointer, TicketKey ticket, BranchName branch, Landed? landedOn,
        CancellationToken ct)
    {
        var repo = project.Repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        if (repo is null) return new SubSeriesRead(name, SubSeriesState.NotCheckedOut);
        var source = sources.Create(repo);
        var read = await TryReadAsync(source, name, pointer, ticket, branch, ct);
        if (landedOn is null || read.State == SubSeriesState.Found) return read;
        // 2026-11-27-ecf2d: the ticket branch is gone (a host throws, or answers nothing) — read where the ticket landed.
        BranchName landed;
        try
        {
            landed = new BranchName(landedOn.SeriesBranch
                ?? (string.IsNullOrWhiteSpace(repo.DefaultBranch) ? (await source.GetDefaultBranchAsync(ct)).Value : repo.DefaultBranch));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The default branch of {Repo} could not be named for ticket {Ticket}", name, ticket.Value);
            return SubSeriesRead.Failed(name, $"the default branch could not be named ({ex.Message.Split('\n', 2)[0].Trim()})");
        }
        return await TryReadAsync(source, name, pointer, ticket, landed, ct);
    }

    private async Task<SubSeriesRead> TryReadAsync(
        ISourceProvider source, string name, SpecSetPointer pointer, TicketKey ticket, BranchName branch, CancellationToken ct)
    {
        try
        {
            var read = await subSeries.ReadAsync(new BranchFiles(source, branch), name, ticket, ct);
            return read.State == SubSeriesState.Found
                ? read with { Sha = pointer.ShaByRepo.GetValueOrDefault(name) ?? string.Empty }
                : read;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Branch {Branch} of {Repo} could not be read for ticket {Ticket}", branch.Value, name, ticket.Value);
            return SubSeriesRead.Failed(name, $"branch {branch.Value} could not be read ({ex.Message.Split('\n', 2)[0].Trim()})");
        }
    }

    private static IReadOnlyList<string> RepoNames(SpecSetPointer pointer) =>
        [.. new[] { pointer.CarryingRepo }.Concat(pointer.ShaByRepo.Keys)
            .Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.Ordinal)];
}
