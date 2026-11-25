namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace as a backend holds it — one pod, one Docker volume set, one host directory —
/// with a container per toolchain image, each its own agent and job id. A container sandbox runs steps and reads its
/// own exit. Disposing the workspace ends every container's agent and then removes what they share, once; nothing
/// else disposes a container.
/// </summary>
public interface IWorkspace : IAsyncDisposable
{
    /// <summary>The toolchain images it has a container for, in the order they were asked for.</summary>
    IReadOnlyList<string> Images { get; }

    /// <summary>
    /// Whether a step for <paramref name="image"/> has a container to run in. A container backend serves the images it
    /// was created with; the in-process backend runs every step on the host and serves any.
    /// </summary>
    bool Serves(string image);

    /// <summary>The container of <paramref name="image"/>; throws when the workspace does not serve it.</summary>
    ISandbox Container(string image);
}
