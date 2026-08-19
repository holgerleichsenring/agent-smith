using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0466: records where a phase of the sequence stands — in the run's own per-phase
/// table, and on the event stream so the server holds a row for the phase.
/// <para>
/// Selecting a phase and verifying it are two handlers writing the same fact, and until
/// now each did it with its own private copy of the update. One writer means the phase
/// cannot be recorded in one place and not the other.
/// </para>
/// </summary>
public interface IPhaseProgressRecorder
{
    Task RecordAsync(
        PipelineContext pipeline,
        string phaseId,
        PhaseRunState state,
        string? failingCommand = null,
        string? note = null,
        CancellationToken cancellationToken = default);
}
