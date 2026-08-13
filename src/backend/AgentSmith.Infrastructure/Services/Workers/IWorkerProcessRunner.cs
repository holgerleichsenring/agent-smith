namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: runs one external-worker invocation — hand it a prompt, get back what the
/// worker printed. The seam exists so the deterministic harness can answer calls without
/// a subprocess while the bridge above it (compose → render → parse → translate) stays
/// the same code a live CLI-driven run uses.
/// </summary>
public interface IWorkerProcessRunner
{
    Task<WorkerProcessResult> RunAsync(
        string prompt, ExternalWorkerCliOptions options, CancellationToken cancellationToken);
}
