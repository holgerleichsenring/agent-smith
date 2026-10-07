using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0466: a finished spec, addressable. The list says which specs the run executed and
/// where each ended up; the single-spec read adds the spec body it executed.
/// <para>
/// The run's execution used to be readable only as a live stream, which is why a spec
/// that had ended could not be opened: a document can be reopened, a stream cannot.
/// 2026-10-06-03c7g: served at /specs with keys specs/specId; was /phases.
/// </para>
/// </summary>
internal static class RunSpecQueryEndpoints
{
    internal static WebApplication MapRunSpecQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/specs", GetRunSpecsAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/runs/{runId}/specs/{specId}", GetRunSpecAsync).Needs(Permissions.RunsRead);
        return app;
    }

    internal static async Task<IResult> GetRunSpecsAsync(
        string runId, RunSpecsReader specs, CancellationToken cancellationToken)
    {
        var all = await specs.ReadAsync(runId, cancellationToken);
        return Results.Ok(new { specs = all });
    }

    internal static async Task<IResult> GetRunSpecAsync(
        string runId, string specId, RunSpecsReader specs, CancellationToken cancellationToken)
    {
        var spec = await specs.ReadOneAsync(runId, specId, cancellationToken);
        return spec is null ? Results.NotFound() : Results.Ok(spec);
    }
}
