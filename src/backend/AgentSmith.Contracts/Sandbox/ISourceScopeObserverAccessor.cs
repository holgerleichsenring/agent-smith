namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// The observer carried ambiently for the work that set it — the way the run context is.
/// Scopes are created down more than one route (a factory call, the shared template
/// selection), and the observer reaches every one of them without either route's signature
/// naming it.
/// <para>
/// The default is no observer, and a scope with none reports nothing: only a caller that
/// wants progress sets one, so every other run behaves as it did.
/// </para>
/// </summary>
public interface ISourceScopeObserverAccessor
{
    /// <summary>The observer set on this async flow, or null when none is.</summary>
    ISourceScopeObserver? Current { get; }

    /// <summary>Sets <paramref name="observer"/> until the returned handle is disposed.</summary>
    IDisposable Observe(ISourceScopeObserver observer);
}
