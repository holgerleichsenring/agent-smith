using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static AgentSmith.Server.Services.Config.ConfigStudioWriteGuard;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0353: the global SETTINGS singletons — one typed form per settings doc in the
/// studio. A settings save is entity CRUD by another name: same write guard, same
/// attributed ConfigChange, same live epoch bump.
/// </summary>
internal static class ConfigSettingsEndpoints
{
    internal static WebApplication MapConfigSettingsEndpoints(this WebApplication app)
    {
        // p0353: GET the exposed type list + each assembled value; PUT saves the
        // doc through GuardSignalingAsync, so a settings change records an attributed,
        // revertible ConfigChange AND bumps the epoch + publishes ConfigChangedEvent —
        // it shows in Changes and applies live (poller + enforcers re-read), exactly
        // like entity CRUD. An unknown/non-editable type is a 404, a malformed doc a 400.
        app.MapGet("/api/config/settings", (IConfigStore store) => Results.Ok(Editable(store)))
           .Needs(Permissions.ConfigRead);

        app.MapGet("/api/config/settings/{type}", (string type, IConfigStore store) =>
            Editable(store).Contains(type)
                ? Results.Ok(store.GetSetting(type))
                : Results.NotFound(new { error = $"Unknown settings type '{type}'." }))
           .Needs(Permissions.ConfigRead);

        app.MapPut("/api/config/settings/{type}",
            async (string type, [FromBody] JsonElement doc, IConfigStore store,
                [FromServices] IConfigReloadSignal reload, [FromServices] ISystemEventPublisher events, HttpContext ctx) =>
            {
                if (!Editable(store).Contains(type))
                    return Results.NotFound(new { error = $"Unknown settings type '{type}'." });
                return await GuardSignalingAsync(ctx, reload, events,
                    () => { store.SaveSetting(type, doc, Attribution(ctx)); return Results.Ok(store.GetSetting(type)); });
            }).Needs(Permissions.ConfigWrite);

        return app;
    }

    /// <summary>
    /// 2026-08-26-7a51: the role mapping left this route. It is still a settings singleton
    /// in the store — that is what gives it the change row, the revert and the export — but
    /// granting a role decides <c>access.write</c> now instead of inheriting
    /// <c>config.write</c>, and a type reachable from both would be neither.
    /// </summary>
    private static IReadOnlyList<string> Editable(IConfigStore store) =>
        [.. store.SettingTypes.Where(type => type != ConfigDocTypes.RoleMapping)];
}
