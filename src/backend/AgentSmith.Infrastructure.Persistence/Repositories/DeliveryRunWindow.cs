using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-25-0099b: the code runs of tickets whose id falls in [fromId, toId), in the caller's projects. A run id
/// begins with its UTC instant, so the window is a text range SQL compares on every provider, where a
/// DateTimeOffset is not comparable on SQLite.
/// <para>2026-10-26-7000c: and, apart from them, the spec derivations of tickets in the same window and scope.</para>
/// </summary>
public static class DeliveryRunWindow
{
    public static IQueryable<Run> InWindow(this IQueryable<Run> runs, ProjectScope scope, string fromId, string toId) =>
        runs.Of(PipelinePresets.CodeName, scope, fromId, toId);

    public static IQueryable<Run> DerivationsInWindow(this IQueryable<Run> runs, ProjectScope scope, string fromId, string toId) =>
        runs.Of(PipelinePresets.SpecDeriveName, scope, fromId, toId);

    private static IQueryable<Run> Of(this IQueryable<Run> runs, string pipeline, ProjectScope scope, string fromId, string toId) =>
        runs.Where(r => r.Pipeline == pipeline && r.TicketId != ""
                        && string.Compare(r.Id, fromId) >= 0 && string.Compare(r.Id, toId) < 0)
            .Within(scope);
}
