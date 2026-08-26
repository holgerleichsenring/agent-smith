using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-26-7a51: the access surface — who may do what, in four panes over one document.
/// <para>
/// Its own permissions rather than <c>config.write</c>: a custom role bundling
/// <c>config.write</c> is legal, and granting a role is how such a caller would become an
/// administrator and collect the secrets permissions the catalog kept separable.
/// <c>access.read</c> and <c>access.write</c> are held by <c>admin</c> alone.
/// </para>
/// </summary>
internal static class AccessEndpoints
{
    internal static WebApplication MapAccessEndpoints(this WebApplication app)
    {
        app.MapGet("/api/access",
            async ([FromServices] AccessSurfaceReader reader, CancellationToken ct) =>
                Results.Ok(await reader.ViewAsync(ct)))
           .Needs(Permissions.AccessRead);

        // The WHOLE document, because a settings write binds onto a fresh model: a
        // people-only body would revert the claim names to their defaults and delete the
        // custom roles this surface promises to keep.
        app.MapPut("/api/access",
            ([FromBody] JsonElement doc, [FromServices] AccessGrantWriter writer,
                [FromServices] AccessSurfaceReader reader, [FromServices] IConfigReloadSignal reload,
                [FromServices] ISystemEventPublisher events, HttpContext ctx, CancellationToken ct) =>
            GuardSignalingAsync(ctx, reload, events, async () =>
            {
                writer.Save(doc, Attribution(ctx));
                return Results.Ok(await reader.ViewAsync(ct));
            })).Needs(Permissions.AccessWrite);

        // One action, because a grant and an observation are different kinds of thing and
        // an administrator removing a person means both.
        app.MapDelete("/api/access/people/{id}",
            (string id, [FromServices] PersonRemover remover, [FromServices] AccessSurfaceReader reader,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events,
                HttpContext ctx, CancellationToken ct) =>
            GuardSignalingAsync(ctx, reload, events, async () =>
                await remover.RemoveAsync(id, Attribution(ctx), ct)
                    ? Results.Ok(await reader.ViewAsync(ct))
                    : Results.NotFound(new { error = $"Nothing here names '{id}'." })))
           .Needs(Permissions.AccessWrite);

        return app;
    }
}
