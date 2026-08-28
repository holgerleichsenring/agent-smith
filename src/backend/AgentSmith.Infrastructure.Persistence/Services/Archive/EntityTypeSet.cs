using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: reaches a table by its <see cref="IEntityType"/> rather than by a
/// generic parameter, so the archive can walk all twenty-two tables in a loop instead of
/// naming each one. The generic set is still what runs — the model's own query pipeline,
/// no hand-written SQL and no provider dialect.
/// </summary>
public sealed class EntityTypeSet
{
    private static readonly MethodInfo RowsMethod = Method(nameof(StreamRows));
    private static readonly MethodInfo CountMethod = Method(nameof(CountRows));
    private static readonly MethodInfo AnyMethod = Method(nameof(AnyRow));

    /// <summary>Every row of the table, untracked, so a large table streams.</summary>
    public IAsyncEnumerable<object> Rows(AgentSmithDbContext db, IEntityType type) =>
        (IAsyncEnumerable<object>)Invoke(RowsMethod, type, [db]);

    public Task<long> CountAsync(AgentSmithDbContext db, IEntityType type, CancellationToken ct) =>
        (Task<long>)Invoke(CountMethod, type, [db, ct]);

    public Task<bool> AnyAsync(AgentSmithDbContext db, IEntityType type, CancellationToken ct) =>
        (Task<bool>)Invoke(AnyMethod, type, [db, ct]);

    private static IAsyncEnumerable<object> StreamRows<T>(AgentSmithDbContext db) where T : class =>
        db.Set<T>().AsNoTracking().AsAsyncEnumerable();

    private static Task<long> CountRows<T>(AgentSmithDbContext db, CancellationToken ct) where T : class =>
        db.Set<T>().LongCountAsync(ct);

    private static Task<bool> AnyRow<T>(AgentSmithDbContext db, CancellationToken ct) where T : class =>
        db.Set<T>().AnyAsync(ct);

    private static object Invoke(MethodInfo method, IEntityType type, object?[] arguments) =>
        method.MakeGenericMethod(type.ClrType).Invoke(null, arguments)!;

    private static MethodInfo Method(string name) =>
        typeof(EntityTypeSet).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
}
