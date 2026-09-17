namespace AgentSmith.Contracts.Turns;

/// <summary>
/// The activity observer carried ambiently for the work that set it — the way the run
/// context and the source-scope observer are. A turn's steps happen in the tool wrapper, the
/// chat client and two re-prompts, and an observer carried this way reaches all four without
/// a run id, a registry or a signature change on any of them.
/// <para>
/// The default is no observer, and everything reports to nothing: only a caller that wants
/// its own progress sets one, so every other run behaves exactly as it did.
/// </para>
/// </summary>
public interface ITurnActivityObserverAccessor
{
    /// <summary>The observer set on this async flow, or null when none is.</summary>
    ITurnActivityObserver? Current { get; }

    /// <summary>Sets <paramref name="observer"/> until the returned handle is disposed.</summary>
    IDisposable Observe(ITurnActivityObserver observer);

    /// <summary>
    /// Reports one step to whoever is listening on this flow, and to nobody otherwise. On the
    /// contract rather than in a helper, so no report site — each of them one line inside code
    /// that exists for something else — has to carry the same null check.
    /// <para>
    /// A DYNAMIC PROXY DOES NOT INHERIT THIS BODY. <c>Mock.Of&lt;ITurnActivityObserverAccessor&gt;()</c>
    /// intercepts it like any other member and returns null for the Task, which every caller
    /// awaits. Hand a test the real accessor (nothing observing it is already silence), not a
    /// mock of this interface.
    /// </para>
    /// </summary>
    Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return Current?.ReportAsync(activity, cancellationToken) ?? Task.CompletedTask;
    }
}
