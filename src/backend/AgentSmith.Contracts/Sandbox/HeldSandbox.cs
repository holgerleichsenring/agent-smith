namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: one sandbox a design conversation is holding open between turns,
/// as the process that holds it knows it.
/// </summary>
/// <param name="Key">
/// What identifies this hold to the turn that may take it back — 2026-09-22-2d11b:
/// <see cref="KeyFor"/> over the conversation, the repository and the revision, because
/// that is all a turn knows before it has anything to read through. The job id could not
/// serve: it belongs to the container the turn is trying to avoid spawning.
/// </param>
/// <param name="ConversationId">The conversation whose label the sandbox carries.</param>
/// <param name="Sandbox">The sandbox itself: read through on a take, force-removed on a release.</param>
public sealed record HeldSandbox(string Key, string ConversationId, IHoldableSandbox Sandbox)
{
    /// <summary>
    /// The hold key of one repository of one conversation, at one revision. The revision is
    /// part of it because a turn may address one repository twice — its own scope at the
    /// clone's default, and a template of the same repository pinned to a tag — and those
    /// are two trees that must never be handed each other's container.
    /// </summary>
    public static string KeyFor(string conversationId, string repoName, string? revision) =>
        $"{conversationId}/{repoName}@{revision ?? string.Empty}";
}
