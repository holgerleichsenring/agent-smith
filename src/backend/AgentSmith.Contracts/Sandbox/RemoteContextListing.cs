namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-08-31-f634: how a remote contexts listing actually went, before the
/// synthetic-default fallback flattens it.
/// <para>
/// The source provider already answers the question honestly — a path that
/// does not exist lists empty, auth and transport errors propagate — and the sandbox
/// resolver then turns BOTH into one root sandbox, because a run has to start either
/// way. A caller that reports rather than resolves must not repeat that flattening:
/// telling an operator to declare verification stages when the truth is that the
/// credential cannot read the repository is the wrong instruction, delivered with
/// confidence.
/// </para>
/// </summary>
/// <param name="Contexts">The contexts read and parsed. Empty with no
/// <paramref name="UnreadableReason"/> means the repository declares none — the normal
/// state of a repository nobody has onboarded yet.</param>
/// <param name="UnreadableReason">Why <c>.agentsmith/contexts</c> could not be listed at
/// all. Null when the listing succeeded, whatever it contained.</param>
public sealed record RemoteContextListing(
    IReadOnlyList<RemoteContextDiscovery> Contexts,
    string? UnreadableReason = null)
{
    public static RemoteContextListing None { get; } = new([]);

    public static RemoteContextListing Unreadable(string reason) => new([], reason);

    public bool IsUnreadable => UnreadableReason is not null;
}
