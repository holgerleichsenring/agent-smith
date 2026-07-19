using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0345: the config studio's WRITE surface over <see cref="IConfigStore"/>. CRUD
/// per catalog entity plus the attributed change feed and revert. Referential
/// integrity is enforced in the store (unknown agent/tracker/repo ref on a project
/// → <see cref="ConfigurationException"/> surfaced here as 400). Mapped only inside
/// Program.cs's <c>AGENTSMITH_UI_API_ENABLED</c> block, like the other dashboard
/// endpoints, so a dashboard-less deployment never exposes the mutation surface.
/// </summary>
internal static class ConfigStudioEndpoints
{
    internal static WebApplication MapConfigStudioEndpoints(this WebApplication app)
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

        // p0345c: the backend-truth capabilities descriptor the studio's forms
        // render from. Type/strategy/pipeline lists come from the enums + code-
        // defined presets; agent providers from the REGISTERED chat-client
        // builders — the same index ChatClientFactory resolves against.
        app.MapGet("/api/config/capabilities", ([FromServices] IEnumerable<IChatClientBuilder> builders) =>
            Results.Ok(ConfigStudioCapabilities.Build(builders.SelectMany(b => b.SupportedTypes))));

        // p0345c: the repo picker's discovery cache — the p0281a last-good snapshot.
        // Unknown connection → 404; known-but-undiscovered → 200 with
        // discoveredAt null + empty repos (honest "not discovered yet").
        app.MapGet("/api/config/connections/{id}/repos",
            async (string id, IConfigStore store,
                [FromServices] IConnectionRepoSnapshotStore snapshots, CancellationToken ct) =>
            {
                if (store.GetConnections().All(c => c.Id != id))
                    return Results.NotFound(new { error = $"Unknown connection '{id}'." });
                var discovery = await snapshots.TryGetDiscoveryAsync(id, ct);
                return Results.Ok(new ConnectionReposView(
                    discovery?.DiscoveredAt,
                    discovery?.Repos.Select(r => new ConnectionRepoView(r.Name, r.DefaultBranch)).ToList()
                        ?? []));
            });

        // p0343b: the studio's "Export agentsmith.yml" — the canonical catalog as
        // loader-round-trippable YAML, served as a download.
        app.MapGet("/api/config/export.yml", (IConfigStore store) =>
            Results.Text(store.ExportYaml(), "text/yaml"));

        // p0352: the studio's "Import agentsmith.yml" — the DR/cutover counterpart of
        // export, over the DB entity-document store. Guarded like the CLI: an empty
        // store imports freely, a non-empty one needs ?force=true (409 otherwise, so
        // the UI can confirm-overwrite and retry). persistence is bootstrap-only
        // (read from file/env before the DB), so it is never imported.
        app.MapPost("/api/config/import",
            async (HttpRequest req, [FromServices] IConfigDocumentStore docStore, IConfigStore store, HttpContext ctx) =>
            {
                var force = req.Query["force"] == "true";
                using var reader = new StreamReader(req.Body);
                var yaml = await reader.ReadToEndAsync();
                if (!force && !docStore.IsEmpty())
                    return Results.Conflict(new
                    {
                        error = "Config store is not empty; confirm to overwrite it (versions are bumped, history kept).",
                    });
                return Guard(() =>
                {
                    var raw = RawConfigYaml.Deserialize(yaml);
                    var writes = new ConfigDocumentAssembler().Decompose(raw)
                        .Where(d => d.Type != ConfigDocTypes.Persistence)
                        .Select(d => new ConfigDocWrite(
                            d.Type, d.Id, d.Doc, ExpectedVersion: null, d.Edges, Attribution(ctx).Actor))
                        .ToList();
                    docStore.Import(writes, force);
                    store.Load();
                    return Results.Ok(new { imported = writes.Count });
                });
            });

        app.MapGet("/api/config/changes", (IConfigStore store) => Results.Ok(store.GetChanges()));
        app.MapPost("/api/config/changes/{id}/revert", (string id, IConfigStore store, HttpContext ctx) =>
            Guard(() => { store.Revert(id, Attribution(ctx)); return Results.NoContent(); }));

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

        app.MapPost(basePath, ([FromBody] TEntity entity, IConfigStore store, HttpContext ctx) =>
            Guard(() => { upsert(store, entity, Attribution(ctx)); return Results.Ok(entity); }));

        app.MapPut(basePath + "/{id}", (string id, [FromBody] TEntity entity, IConfigStore store, HttpContext ctx) =>
            Guard(() =>
            {
                var withRouteId = withId(entity, id);
                upsert(store, withRouteId, Attribution(ctx));
                return Results.Ok(withRouteId);
            }));

        app.MapDelete(basePath + "/{id}", (string id, IConfigStore store, HttpContext ctx) =>
            Guard(() => { delete(store, id, Attribution(ctx)); return Results.NoContent(); }));
    }

    private static ChangeAttribution Attribution(HttpContext ctx)
    {
        var actor = ctx.Request.Headers["X-Actor"].FirstOrDefault();
        return new ChangeAttribution(string.IsNullOrWhiteSpace(actor) ? "dashboard" : actor!);
    }

    private static IResult Guard(Func<IResult> action)
    {
        try
        {
            return action();
        }
        catch (StaleConfigVersionException ex)
        {
            // p0349: a concurrent edit moved the entity's version on — 409, never a
            // silent last-write-wins. The client reloads and retries.
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ConfigurationException ex)
        {
            // Referential integrity / validation failure — a client error, not a 500.
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
