using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0466: records where a spec of the sequence stands — in the run's own per-spec
/// table, and on the event stream so the server holds a row for the spec.
/// <para>
/// Selecting a spec and verifying it are two handlers writing the same fact, and until
/// now each did it with its own private copy of the update. One writer means the spec
/// cannot be recorded in one place and not the other.
/// </para>
/// </summary>
public interface ISpecProgressRecorder
{
    Task RecordAsync(
        PipelineContext pipeline,
        string specId,
        PhaseRunState state,
        string? failingCommand = null,
        string? note = null,
        CancellationToken cancellationToken = default);
}
