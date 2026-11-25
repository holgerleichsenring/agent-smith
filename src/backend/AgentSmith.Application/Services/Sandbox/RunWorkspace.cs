using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the run's workspace as the run holds it — the one the backend created and, after a restart
/// with one more toolchain, the one that replaced it. Every sandbox key of the run reaches its toolchain container
/// through here, so a key handed out before a restart reaches the new container after it.
/// </summary>
public sealed class RunWorkspace(IWorkspaceFactory factory) : IAsyncDisposable
{
    private IWorkspace? _current;

    public WorkspaceStepGate Gate { get; } = new();

    /// <summary>What the current workspace was created from; null before the first one.</summary>
    public WorkspaceSpec? Spec { get; private set; }

    public bool IsCreated => _current is not null;

    /// <summary>The images the current workspace has a container for.</summary>
    public IReadOnlyList<string> Images => _current?.Images ?? [];

    /// <summary>Whether the current workspace has a container for <paramref name="image"/>.</summary>
    public bool Serves(string image) => _current?.Serves(image) == true;

    public async Task CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        if (_current is not null) throw new InvalidOperationException("The run's workspace exists already.");
        _current = await factory.CreateAsync(spec, cancellationToken);
        Spec = spec;
    }

    /// <summary>Removes the current workspace and creates <paramref name="spec"/>'s in its place.</summary>
    public async Task ReplaceAsync(WorkspaceSpec spec, CancellationToken cancellationToken)
    {
        if (_current is { } old)
        {
            _current = null;
            await old.DisposeAsync();
        }
        _current = await factory.CreateAsync(spec, cancellationToken);
        Spec = spec;
    }

    public ISandbox Container(string image) =>
        (_current ?? throw new InvalidOperationException("The run's workspace is not running.")).Container(image);

    public async ValueTask DisposeAsync()
    {
        if (_current is not { } current) return;
        _current = null;
        await current.DisposeAsync();
    }
}
