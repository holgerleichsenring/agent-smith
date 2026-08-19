using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0466: a finished phase, addressable. The list says which phases the run had and
/// where each ended up; the single-phase read adds the spec it executed.
/// <para>
/// The run's execution used to be readable only as a live stream, which is why a phase
/// that had ended could not be opened: a document can be reopened, a stream cannot.
/// </para>
/// </summary>
internal static class RunPhaseQueryEndpoints
{
    internal static WebApplication MapRunPhaseQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/phases", GetRunPhasesAsync);
        app.MapGet("/api/runs/{runId}/phases/{phaseId}", GetRunPhaseAsync);
        return app;
    }

    internal static async Task<IResult> GetRunPhasesAsync(
        string runId, RunPhasesReader phases, CancellationToken cancellationToken)
    {
        var all = await phases.ReadAsync(runId, cancellationToken);
        return Results.Ok(new { phases = all });
    }

    internal static async Task<IResult> GetRunPhaseAsync(
        string runId, string phaseId, RunPhasesReader phases, CancellationToken cancellationToken)
    {
        var phase = await phases.ReadOneAsync(runId, phaseId, cancellationToken);
        return phase is null ? Results.NotFound() : Results.Ok(phase);
    }
}
