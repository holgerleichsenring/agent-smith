using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Mvc;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0343b/p0352: the config studio's whole-catalog transfer pair — export the canonical
/// catalog as loader-round-trippable YAML, and import one back. The DR/cutover seam;
/// everything else in the studio edits a single entity.
/// </summary>
internal static class ConfigTransferEndpoints
{
    internal static WebApplication MapConfigTransferEndpoints(this WebApplication app)
    {
        // p0343b: the studio's "Export agentsmith.yml" — the canonical catalog as
        // loader-round-trippable YAML, served as a download.
        app.MapGet("/api/config/export.yml", (IConfigStore store) =>
            Results.Text(store.ExportYaml(), "text/yaml"))
           .Needs(Permissions.ConfigExport, Permissions.SecretsRead);

        // p0352: the studio's "Import agentsmith.yml" — the DR/cutover counterpart of
        // export, over the DB entity-document store. Guarded like the CLI: an empty
        // store imports freely, a non-empty one needs ?force=true (409 otherwise, so
        // the UI can confirm-overwrite and retry). persistence is bootstrap-only
        // (read from file/env before the DB), so it is never imported.
        app.MapPost("/api/config/import",
            async (HttpRequest req, [FromServices] IConfigDocumentStore docStore, IConfigStore store,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
            {
                var force = req.Query["force"] == "true";
                using var reader = new StreamReader(req.Body);
                var yaml = await reader.ReadToEndAsync();
                if (!force && !docStore.IsEmpty())
                    return Results.Conflict(new
                    {
                        error = "Config store is not empty; confirm to overwrite it (versions are bumped, history kept).",
                    });
                return await GuardSignalingAsync(ctx, reload, events, () =>
                {
                    var raw = new RawConfigYaml().Deserialize(yaml);
                    var writes = new ConfigDocumentAssembler().Decompose(raw)
                        .Where(d => d.Type != ConfigDocTypes.Persistence)
                        .Select(d => new ConfigDocWrite(
                            d.Type, d.Id, d.Doc, ExpectedVersion: null, d.Edges, Attribution(ctx).Actor))
                        .ToList();
                    docStore.Import(writes, force);
                    store.Load();
                    return Results.Ok(new { imported = writes.Count });
                });
            }).Needs(Permissions.ConfigImport, Permissions.SecretsWrite);

        return app;
    }
}
