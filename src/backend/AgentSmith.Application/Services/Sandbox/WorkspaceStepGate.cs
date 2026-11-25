namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the steps of a run's workspace share it; a restart that replaces the workspace holds it alone.
/// A step that arrives while a restart holds it waits for the restart to finish, however long; a restart waits for
/// the steps already running — a long build holds it back until it ends. The restart's own steps (the checkpoint
/// push, the clones that restore the repositories) run on the restart's flow and pass.
/// </summary>
public sealed class WorkspaceStepGate
{
    private static readonly AsyncLocal<bool> HoldsIt = new();
    private readonly object _sync = new();
    private int _running;
    private TaskCompletionSource? _exclusive;
    private TaskCompletionSource? _drained;

    /// <summary>Admits one step; dispose the result when the step has ended.</summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        if (HoldsIt.Value) return Pass.Instance;
        while (true)
        {
            Task closed;
            lock (_sync)
            {
                if (_exclusive is null)
                {
                    _running++;
                    return new Admitted(this);
                }
                closed = _exclusive.Task;
            }
            await closed.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Runs <paramref name="work"/> once no other step runs, with every other step held at the gate.</summary>
    public async Task<T> RunAloneAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        TaskCompletionSource drained;
        lock (_sync)
        {
            if (_exclusive is not null) throw new InvalidOperationException("The workspace is already being restarted.");
            _exclusive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _drained = drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_running == 0) drained.SetResult();
        }
        try
        {
            await drained.Task.WaitAsync(cancellationToken);
            HoldsIt.Value = true;
            return await work();
        }
        finally
        {
            HoldsIt.Value = false;
            TaskCompletionSource open;
            lock (_sync) { open = _exclusive!; _exclusive = null; _drained = null; }
            open.SetResult();
        }
    }

    private void Leave()
    {
        lock (_sync)
            if (--_running == 0) _drained?.TrySetResult();
    }

    private sealed class Admitted(WorkspaceStepGate gate) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0) gate.Leave();
        }
    }

    private sealed class Pass : IDisposable
    {
        public static readonly Pass Instance = new();

        public void Dispose() { }
    }
}
