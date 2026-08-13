using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: an external-worker call that did not produce a usable answer — a non-zero exit,
/// a timeout, an empty answer, an unparseable reply, an invented tool. Every one of them
/// names the request, the run and the step, because the failure mode this bridge must
/// never have is a silent empty response the loop then reasons about.
/// </summary>
public sealed class ExternalWorkerCallException(WorkerRequest request, string reason, TimeSpan duration)
    : Exception($"External worker call failed after {duration.TotalSeconds:F1}s — "
        + $"{request.Describe()}: {reason}")
{
    public WorkerRequest Request { get; } = request;
    public string Reason { get; } = reason;
    public TimeSpan Duration { get; } = duration;
}
