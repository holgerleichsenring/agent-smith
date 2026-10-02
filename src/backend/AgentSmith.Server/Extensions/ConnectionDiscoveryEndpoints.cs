using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Config;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-02-5f89c: a connection's repo discovery in the studio — the last success, the last
/// error with its reason, the repo count — and Refresh now. Reading is config.read; a key nothing
/// was recorded under is discovered at once. Refresh now is an outbound authenticated call with
/// the installation's credentials, so it takes diagnostics.probe, like the template context lookup.
/// Unknown connection → 404 on both.
/// </summary>
internal static class ConnectionDiscoveryEndpoints
{
    internal static WebApplication MapConnectionDiscoveryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/connections/{id}/repos",
            async (string id, [FromServices] ConnectionDiscoveryReader reader, CancellationToken ct) =>
                await reader.ReadAsync(id, ct) is { } view ? Results.Ok(view) : Unknown(id))
           .Needs(Permissions.ConfigRead);

        app.MapPost("/api/config/connections/{id}/discovery/refresh",
            async (string id, [FromServices] ConnectionDiscoveryReader reader, CancellationToken ct) =>
                await reader.RefreshNowAsync(id, ct) is { } view ? Results.Ok(view) : Unknown(id))
           .Needs(Permissions.DiagnosticsProbe);

        return app;
    }

    private static IResult Unknown(string id) => Results.NotFound(new { error = $"Unknown connection '{id}'." });
}
