using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: the hand-back the ticket's spec set LAST recorded, read the same way the
/// derivation writes it — by project and <c>SpecSetKey.For(platform, ticket id)</c>.
/// <para>
/// The QUESTION itself is a ticket comment and is not stored here; the server keeps only the
/// case and how often it repeated (TicketSpecSet), so the row names the case and links to the
/// ticket for the words.
/// </para>
/// </summary>
public sealed class FiledWorkHandbacks(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// The most recently written hand-back across the tracker's projects, or null where none
    /// was recorded. A run is spawned per matching project, so the set may be stored under any
    /// of them and two may both hold one — an old uncleared case in the first and this
    /// morning's in the second. Insertion order would name the wrong one, so the row is chosen
    /// by when it was last WRITTEN. A case of <c>None</c> is not a hand-back.
    /// </summary>
    public async Task<FiledWorkHandbackView?> ForAsync(
        IReadOnlyList<FiledWorkProject> projects, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0) return null;
        var keys = projects
            .Select(p => new SpecSetAddress(p.Name, SpecSetKey.For(p.Platform, ticketId).Value))
            .ToList();
        var handed = (await RowsAsync(keys, ct))
            .Where(r => keys.Any(k => k.Project == r.Project && k.Key == r.SpecKey))
            .Where(r => r.LastHandbackCase != (int)SpecHandbackCase.None)
            // UpdatedAt is stamped on every save, so the newest write is the standing case.
            .OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id)
            .FirstOrDefault();
        return handed is null
            ? null
            : new FiledWorkHandbackView(
                ((SpecHandbackCase)handed.LastHandbackCase).ToString(), handed.RepeatedHandbackCount);
    }

    private async Task<List<TicketSpecSet>> RowsAsync(
        List<SpecSetAddress> keys, CancellationToken ct)
    {
        var names = keys.Select(k => k.Project).ToList();
        var values = keys.Select(k => k.Key).ToList();
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await uow.Set<TicketSpecSet>().AsNoTracking()
            .Where(r => names.Contains(r.Project) && values.Contains(r.SpecKey))
            .ToListAsync(ct);
    }

    /// <summary>One project and the spec-set key this ticket's set would live under there.</summary>
    private sealed record SpecSetAddress(string Project, string Key);
}
