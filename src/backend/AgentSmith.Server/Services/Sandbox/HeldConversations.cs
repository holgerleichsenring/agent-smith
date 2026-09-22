namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the conversations ONE scan may not judge a sandbox dead for.
/// <para>
/// <see cref="SpareAll"/> is what an unanswerable read yields — the session store
/// could not be read, or the hold window could not be resolved even once. Sparing
/// everything it could not judge is the safe direction: a sandbox kept one scan too
/// long is collected on the next one, while a held sandbox removed wrongly takes a
/// live design turn's source with it.
/// </para>
/// </summary>
public sealed record HeldConversations(IReadOnlySet<string> Ids, bool SpareAll)
{
    /// <summary>Nothing is held — what a scan with no labelled sandbox sees.</summary>
    public static HeldConversations None { get; } = new(new HashSet<string>(StringComparer.Ordinal), false);

    /// <summary>The read failed; every labelled sandbox is spared this scan.</summary>
    public static HeldConversations Unanswered { get; } =
        new(new HashSet<string>(StringComparer.Ordinal), true);

    public static HeldConversations Of(IEnumerable<string> conversationIds) =>
        new(new HashSet<string>(conversationIds, StringComparer.Ordinal), false);

    public bool IsHeld(string conversationId) => SpareAll || Ids.Contains(conversationId);
}
