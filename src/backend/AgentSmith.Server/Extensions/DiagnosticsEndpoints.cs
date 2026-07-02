using AgentSmith.Server.Services.Diagnostics;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0292: the dashboard's ACTIVE connectivity surface — the complement to
/// <see cref="ConfigQueryEndpoints"/>'s static "how it's wired" view. Runs
/// read-only probes against every configured repo + tracker on demand and
/// reports the webhook panel. Mapped only inside Program.cs's
/// <c>AGENTSMITH_UI_API_ENABLED</c> block, so a dashboard-less deployment never
/// exposes a token-exercising endpoint.
/// </summary>
internal static class DiagnosticsEndpoints
{
    internal static WebApplication MapDiagnosticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/diagnostics/connections",
            async (IConnectionDiagnosticsService diagnostics, CancellationToken ct) =>
                Results.Ok(await diagnostics.GetSnapshotAsync(ct)));

        app.MapPost("/api/diagnostics/connections/{name}/probe",
            async (string name, IConnectionDiagnosticsService diagnostics, CancellationToken ct) =>
            {
                var status = await diagnostics.ProbeAsync(name, ct);
                return status is null ? Results.NotFound() : Results.Ok(status);
            });

        return app;
    }
}
