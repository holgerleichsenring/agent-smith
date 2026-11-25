using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-11-25-9992b: the stub workspace — its containers by image; disposing it disposes each once.</summary>
internal sealed class StubWorkspace(WorkspaceSpec spec, IReadOnlyList<string> images, IReadOnlyList<StubSandbox> containers)
    : IWorkspace
{
    public WorkspaceSpec Spec => spec;

    public IReadOnlyList<string> Images => images;

    public IReadOnlyList<StubSandbox> Containers => containers;

    /// <summary>How often the owner disposed it; the containers end on the first.</summary>
    public int DisposeCount { get; private set; }

    public bool Serves(string image) => images.Contains(image, StringComparer.Ordinal);

    public ISandbox Container(string image)
    {
        var index = images.ToList().IndexOf(image);
        return index >= 0
            ? containers[index]
            : throw new InvalidOperationException($"The stub workspace has no container for '{image}'.");
    }

    public async ValueTask DisposeAsync()
    {
        if (DisposeCount++ > 0) return;
        foreach (var container in containers) await container.DisposeAsync();
    }
}
