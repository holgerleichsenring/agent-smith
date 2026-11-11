namespace AgentSmith.Contracts.Turns;

/// <summary>
/// 2026-11-10-d854c: the <see cref="ITurnRunRecorder"/> carried ambiently for the turn that set it, as
/// <see cref="ITurnProposalObserverAccessor"/> is. The default is none, so a run nobody listens to records nothing.
/// </summary>
public interface ITurnRunRecorderAccessor
{
    ITurnRunRecorder? Current { get; }

    /// <summary>Sets <paramref name="recorder"/> until the returned handle is disposed.</summary>
    IDisposable Observe(ITurnRunRecorder recorder);
}
