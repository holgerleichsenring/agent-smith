namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Reports on the calling thread. <see cref="Progress{T}"/> posts to the thread pool,
/// so lines can still be in flight when the awaited step returns — fine for a live
/// view, wrong for anyone who reads the collected text right after the await.
/// <para>
/// p0419: two handlers had grown their own private copy of this; it became the third
/// when the verify gate needed one, which is where a duplicated type stops being an
/// accident and starts being a decision.
/// </para>
/// </summary>
public sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
