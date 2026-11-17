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
/// </summary>
public sealed class TicketBranchSeries(
    ISpecSetPointerStore pointers,
    ISourceProviderFactory sources,
    SubSeriesFiles subSeries,
    SeriesComposer composer,
    ILogger<TicketBranchSeries> logger)
{
    public async Task<SpecSetOnBranch> ReadAsync(
        ResolvedProject project, TicketKey ticket, TicketId ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var pointer = await pointers.GetAsync(project.Name, ticket.Value, cancellationToken);
        if (pointer is null) return SpecSetOnBranch.Nothing;
        var branch = TicketBranchNamer.Compose(ticketId);
        var reads = new List<SubSeriesRead>();
        foreach (var name in RepoNames(pointer))
            reads.Add(await ReadAsync(project, name, pointer, ticket, branch, cancellationToken));
        return composer.Compose(ticket, pointer.CarryingRepo, reads);
    }

    private async Task<SubSeriesRead> ReadAsync(
        ResolvedProject project, string name, SpecSetPointer pointer, TicketKey ticket, BranchName branch,
        CancellationToken ct)
    {
        var repo = project.Repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        if (repo is null) return new SubSeriesRead(name, SubSeriesState.NotCheckedOut);
        try
        {
            var read = await subSeries.ReadAsync(new BranchFiles(sources.Create(repo), branch), name, ticket, ct);
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
