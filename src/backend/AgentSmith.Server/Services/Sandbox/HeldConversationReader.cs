using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the reapers' third rail, read once per scan. A sandbox's
/// conversation label names a session; the session is HELD while it is open and its
/// last activity is inside the project's hold window.
/// <para>
/// The join is a label and a shared row, never process state: the owner identity a
/// reaper judges under is the LIVENESS STORE's and not the process's, so two replicas
/// sharing one store clean up after each other and the cluster's corpse sweep is
/// leader-elected. A rail that asked "do I hold this?" would do nothing on exactly the
/// deployments it exists for.
/// </para>
/// <para>
/// A scan whose candidates carry no conversation label reads nothing at all — neither
/// the catalog nor the session store — which is what makes this phase cost nothing
/// until something is actually held.
/// </para>
/// </summary>
public sealed class HeldConversationReader(
    SandboxHoldWindowResolver windows,
    IConversationLivenessReader conversations,
    TimeProvider clock,
    ILogger<HeldConversationReader> logger)
{
    public async Task<HeldConversations> ReadAsync(
        IEnumerable<SandboxReapCandidate> candidates, CancellationToken cancellationToken)
    {
        var conversationIds = Labelled(candidates);
        if (conversationIds.Count == 0) return HeldConversations.None;
        if (windows.ResolveForScan() is not { } resolved) return HeldConversations.Unanswered;
        try
        {
            var now = clock.GetUtcNow();
            var rows = await conversations.ReadAsync(conversationIds, cancellationToken);
            return HeldConversations.Of(rows
                .Where(row => IsHeld(row, resolved, now))
                .Select(row => row.ConversationId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-safe, not fail-open: what could not be judged is spared this scan and
            // judged again on the next one. The alternative removes every held sandbox on
            // a database blip.
            logger.LogWarning(ex,
                "Could not read the conversations of {Count} labelled sandbox(es) — sparing them this scan",
                conversationIds.Count);
            return HeldConversations.Unanswered;
        }
    }

    private static IReadOnlyCollection<string> Labelled(IEnumerable<SandboxReapCandidate> candidates) =>
        [.. candidates
            .Select(candidate => candidate.ConversationId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)];

    private static bool IsHeld(ConversationLiveness row, SandboxHoldWindows windows, DateTimeOffset now)
    {
        var window = windows.For(row.Project);
        return row.IsOpen && window > TimeSpan.Zero && now - row.LastActivityAt <= window;
    }
}
