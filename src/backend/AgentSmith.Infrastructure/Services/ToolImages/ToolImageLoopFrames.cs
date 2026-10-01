namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the ambient <see cref="ToolImageLoopFrame"/> of the tool loop running on
/// this async flow, AsyncLocal like AsyncLocalSourceScopeObserverAccessor. A sub-agent's loop,
/// run from inside a parent's tool call, opens a frame of its own, so what its tools deposit
/// never reaches the parent's history.
/// </summary>
public sealed class ToolImageLoopFrames
{
    private static readonly AsyncLocal<ToolImageLoopFrame?> Frame = new();

    public ToolImageLoopFrame? Current => Frame.Value;

    public IDisposable Open()
    {
        var previous = Frame.Value;
        Frame.Value = new ToolImageLoopFrame();
        return new Restore(previous);
    }

    /// <summary>Restores the enclosing frame, so a nested loop unwinds rather than clears.</summary>
    private sealed class Restore(ToolImageLoopFrame? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Frame.Value = previous;
        }
    }
}
