using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace on a container backend — a toolchain container per image over what they share
/// (the Kubernetes pod, the Docker volume set). Disposing it ends every container's agent, side by side so their
/// shutdown graces overlap, and then removes the shared part once. A container never removes it itself.
/// </summary>
public sealed class ContainerWorkspace(
    IReadOnlyList<WorkspaceToolchain> toolchains,
    IReadOnlyList<ISandbox> containers,
    Func<Task> removeShared,
    ILogger logger) : IWorkspace
{
    private bool _disposed;

    public IReadOnlyList<string> Images { get; } = [.. toolchains.Select(t => t.Image)];

    public bool Serves(string image) => Images.Contains(image, StringComparer.Ordinal);

    public ISandbox Container(string image)
    {
        for (var i = 0; i < Images.Count; i++)
            if (string.Equals(Images[i], image, StringComparison.Ordinal)) return containers[i];
        throw new InvalidOperationException(
            $"The run's workspace has no container for '{image}'; it holds [{string.Join(", ", Images)}].");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Task.WhenAll(containers.Select(EndAsync));
        await removeShared();
    }

    private async Task EndAsync(ISandbox container)
    {
        try { await container.DisposeAsync(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workspace container {JobId} did not end cleanly", container.JobId);
        }
    }
}
