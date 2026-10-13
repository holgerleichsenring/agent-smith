namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-13-0b82b: what a read of a repository through its host says about its bootstrap, before
/// any sandbox exists. Unknown is its own answer — an absent branch or an unreachable host is not
/// proof the repository is uninitialised, so a caller that acts on Incomplete never acts on it.
/// </summary>
public sealed record RemoteBootstrapVerdict(
    RemoteBootstrapState State, IReadOnlyList<MissingBootstrapFile> Missing, string? Reason = null)
{
    public static RemoteBootstrapVerdict Complete { get; } = new(RemoteBootstrapState.Complete, []);

    public static RemoteBootstrapVerdict Unknown(string reason) => new(RemoteBootstrapState.Unknown, [], reason);

    public static RemoteBootstrapVerdict Of(IReadOnlyList<ContextProbeResult> contexts) =>
        contexts.All(c => c.Complete)
            ? Complete
            : new RemoteBootstrapVerdict(RemoteBootstrapState.Incomplete, [.. contexts.SelectMany(c => c.Missing())]);
}
