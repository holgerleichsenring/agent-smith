using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Services.Config;
using Microsoft.AspNetCore.Mvc;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0345: the config studio's per-catalog-entity CRUD — list, create, update by route
/// id, delete — generated once per entity type from one generic registration. Every
/// write runs through <see cref="ConfigStudioWriteGuard"/>, so referential integrity
/// failures raised by the store surface as 400 and a successful write bumps the epoch.
/// </summary>
internal static class ConfigEntityRoutes
{
    internal static WebApplication MapConfigEntityRoutes(this WebApplication app)
    {
        MapEntity<AgentEntity>(app, "agents",
            s => s.GetAgents(), (s, e, by) => s.UpsertAgent(e, by), (s, id, by) => s.DeleteAgent(id, by),
            (e, id) => e with { Id = id });
        MapEntity<TrackerEntity>(app, "trackers",
            s => s.GetTrackers(), (s, e, by) => s.UpsertTracker(e, by), (s, id, by) => s.DeleteTracker(id, by),
            (e, id) => e with { Id = id });
        MapEntity<RepoEntity>(app, "repos",
            s => s.GetRepos(), (s, e, by) => s.UpsertRepo(e, by), (s, id, by) => s.DeleteRepo(id, by),
            (e, id) => e with { Id = id });
        MapEntity<ProjectEntity>(app, "projects",
            s => s.GetProjects(), (s, e, by) => s.UpsertProject(e, by), (s, id, by) => s.DeleteProject(id, by),
            (e, id) => e with { Id = id });
        MapEntity<McpServerEntity>(app, "mcp-servers",
            s => s.GetMcpServers(), (s, e, by) => s.UpsertMcpServer(e, by), (s, id, by) => s.DeleteMcpServer(id, by),
            (e, id) => e with { Id = id });
        MapEntity<SecretEntity>(app, "secrets",
            s => s.GetSecrets(), (s, e, by) => s.UpsertSecret(e, by), (s, id, by) => s.DeleteSecret(id, by),
            (e, id) => e with { Id = id });
        // p0345b: git-host connections (the p0281a discovery catalog) — the
        // entity connection-scoped project repo refs validate against.
        MapEntity<ConnectionEntity>(app, "connections",
            s => s.GetConnections(), (s, e, by) => s.UpsertConnection(e, by), (s, id, by) => s.DeleteConnection(id, by),
            (e, id) => e with { Id = id });

        return app;
    }

    private static void MapEntity<TEntity>(
        WebApplication app,
        string route,
        Func<IConfigStore, IReadOnlyList<TEntity>> getAll,
        Action<IConfigStore, TEntity, ChangeAttribution> upsert,
        Action<IConfigStore, string, ChangeAttribution> delete,
        Func<TEntity, string, TEntity> withId)
    {
        var basePath = $"/api/config/{route}";

        app.MapGet(basePath, (IConfigStore store) => Results.Ok(getAll(store)));

        app.MapPost(basePath, ([FromBody] TEntity entity, IConfigStore store,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
            GuardSignalingAsync(ctx, reload, events,
                () => { upsert(store, entity, Attribution(ctx)); return Results.Ok(entity); }));

        app.MapPut(basePath + "/{id}", (string id, [FromBody] TEntity entity, IConfigStore store,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
            GuardSignalingAsync(ctx, reload, events, () =>
            {
                var withRouteId = withId(entity, id);
                upsert(store, withRouteId, Attribution(ctx));
                return Results.Ok(withRouteId);
            }));

        app.MapDelete(basePath + "/{id}", (string id, IConfigStore store,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
            GuardSignalingAsync(ctx, reload, events,
                () => { delete(store, id, Attribution(ctx)); return Results.NoContent(); }));
    }
}
