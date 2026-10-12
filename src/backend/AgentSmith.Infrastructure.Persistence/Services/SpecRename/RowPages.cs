using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: walks a table in key order, a page at a time, so a store of any size is
/// rewritten without holding it in memory. Keyset paging, not offsets: a rewritten row may still
/// match the filter (a reason text that happens to carry an old word), and an offset would then skip or repeat rows.
/// Safe beside another replica doing the same — both write the same value to the same row.
/// </summary>
public sealed class RowPages
{
    /// <summary>Rows per page.</summary>
    public const int Size = 200;

    /// <summary>Applies <paramref name="rewrite"/> to every row; returns how many it changed.</summary>
    public async Task<int> RewriteAsync<TRow, TKey>(
        AgentSmithDbContext db, IQueryable<TRow> rows, Expression<Func<TRow, TKey>> key,
        Func<TRow, bool> rewrite, CancellationToken ct) where TRow : class
    {
        var changed = 0;
        var keyOf = key.Compile();
        var page = await rows.OrderBy(key).Take(Size).ToListAsync(ct);
        while (page.Count > 0)
        {
            changed += page.Count(rewrite);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            page = await rows.Where(After(key, keyOf(page[^1]))).OrderBy(key).Take(Size).ToListAsync(ct);
        }
        return changed;
    }

    private static Expression<Func<TRow, bool>> After<TRow, TKey>(Expression<Func<TRow, TKey>> key, TKey last)
    {
        var bound = Expression.Constant(last, typeof(TKey));
        Expression after = typeof(TKey) == typeof(string)
            ? Expression.GreaterThan(
                Expression.Call(typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!, key.Body, bound),
                Expression.Constant(0))
            : Expression.GreaterThan(key.Body, bound);
        return Expression.Lambda<Func<TRow, bool>>(after, key.Parameters);
    }
}
