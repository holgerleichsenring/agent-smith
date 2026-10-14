namespace AgentSmith.Application.Services.Sandbox;

/// <summary>What the sandbox tree sync left: every sandbox of a repository on one tree
/// (<see cref="Message"/> null), or the red line naming the repository, path or sandbox.</summary>
public sealed record SandboxTreeSyncResult(string? Message)
{
    public static SandboxTreeSyncResult Level { get; } = new((string?)null);

    public bool IsLevel => Message is null;

    public static SandboxTreeSyncResult Red(string message) => new(message);
}
