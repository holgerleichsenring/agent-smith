using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.AspNetCore.Mvc;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0345/p0353: the config studio's attributed change feed and its undo. Every studio
/// write records a ConfigChange; these two routes let the operator read the diff and
/// put it back.
/// </summary>
internal static class ConfigChangeEndpoints
{
    internal static WebApplication MapConfigChangeEndpoints(this WebApplication app)
    {
        // p0353: map to the field-diff DTO the client expects (timestampUtc/entityKind/
        // action/fields[]); returning the raw record left `fields` undefined and crashed
        // the Changes view.
        app.MapGet("/api/config/changes", (IConfigStore store) =>
            Results.Ok(store.GetChanges().Select(Services.Config.ConfigChangeView.From)));
        app.MapPost("/api/config/changes/{id}/revert",
            (string id, IConfigStore store, [FromServices] IConfigReloadSignal reload,
                [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
                GuardSignalingAsync(ctx, reload, events,
                    () => { store.Revert(id, Attribution(ctx)); return Results.NoContent(); }));

        return app;
    }
}
