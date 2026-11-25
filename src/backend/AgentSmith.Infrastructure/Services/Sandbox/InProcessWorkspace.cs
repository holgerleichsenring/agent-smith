using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the in-process workspace — every step runs on the host, so it serves any image and every image's
/// container is the one shared sandbox. Disposing it deletes the owned root; a mounted host source is never touched.
/// </summary>
public sealed class InProcessWorkspace(IReadOnlyList<string> images, InProcessSandbox sandbox) : IWorkspace
{
    public IReadOnlyList<string> Images => images;

    public bool Serves(string image) => true;

    public ISandbox Container(string image) => sandbox;

    public ValueTask DisposeAsync() => sandbox.DisposeAsync();
}
