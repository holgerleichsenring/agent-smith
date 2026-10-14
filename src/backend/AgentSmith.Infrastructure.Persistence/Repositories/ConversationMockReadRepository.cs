using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-8762r: reads a design conversation's HTML mocks (<see cref="MockRows"/>). A version is
/// addressed as (spec id, number); the number is the rank by id among the spec's pages, resolved here.
/// </summary>
public sealed class ConversationMockReadRepository(IUnitOfWork unitOfWork)
{
    /// <summary>Every version, oldest first, projected without content.</summary>
    public async Task<IReadOnlyList<MockVersion>> ListAsync(string sessionId, CancellationToken ct)
    {
        var shots = Shots(sessionId);
        var rows = await Pages(sessionId)
            .Select(f => new { f.Id, f.SetId, f.RelativePath, f.CreatedAt, HasShot = shots.Any(s => s.SetId == f.SetId) })
            .ToListAsync(ct);
        return [.. rows.GroupBy(r => MockRows.SpecOf(r.RelativePath), StringComparer.Ordinal)
            .SelectMany(spec => spec.OrderBy(r => r.Id).Select((r, i) =>
                (r.Id, Version: new MockVersion(r.SetId, spec.Key, i + 1, MockVersion.NameOf(spec.Key, i + 1), r.CreatedAt, r.HasShot))))
            .OrderBy(v => v.Id).Select(v => v.Version)];
    }

    public async Task<MockPage?> HtmlAsync(string sessionId, string specId, int? version, CancellationToken ct)
    {
        var (row, number) = await ResolveAsync(sessionId, specId, version, ct);
        if (row is null) return null;
        var content = await Pages(sessionId).Where(f => f.Id == row.Id).Select(f => f.Content).FirstAsync(ct);
        return new MockPage(new MockVersion(row.SetId, specId, number, MockVersion.NameOf(specId, number), row.At),
            Encoding.UTF8.GetString(content));
    }

    public async Task<byte[]?> ShotAsync(string sessionId, string specId, int version, CancellationToken ct)
    {
        var (row, _) = await ResolveAsync(sessionId, specId, version, ct);
        return row is null
            ? null
            : await Shots(sessionId).Where(f => f.SetId == row.SetId).Select(f => f.Content).FirstOrDefaultAsync(ct);
    }

    private async Task<(Held? Row, int Version)> ResolveAsync(string sessionId, string specId, int? version, CancellationToken ct)
    {
        var path = MockRows.PathOf(specId);
        var rows = (await Pages(sessionId).Where(f => f.RelativePath == path)
            .Select(f => new Held(f.Id, f.SetId, f.CreatedAt)).ToListAsync(ct)).OrderBy(f => f.Id).ToList();
        var number = version ?? rows.Count;
        return number >= 1 && number <= rows.Count ? (rows[number - 1], number) : (null, 0);
    }

    private sealed record Held(long Id, string SetId, DateTimeOffset At);

    private IQueryable<ReferenceFile> Pages(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().AsNoTracking().Where(MockRows.Page(sessionId));

    private IQueryable<ReferenceFile> Shots(string sessionId) =>
        unitOfWork.Set<ReferenceFile>().AsNoTracking().Where(MockRows.Shot(sessionId));
}
