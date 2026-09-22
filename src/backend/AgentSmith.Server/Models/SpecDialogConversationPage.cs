namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-21-f237b: one page of the caller's conversations, and how many they hold in all.
/// <para>
/// The count is a field rather than an inference. A page that shows a limit's worth of rows has
/// to say so, and "I received exactly the number I asked for" is a guess that is wrong for the
/// caller who has exactly that many — the missing field IS the fix. It costs a COUNT over the
/// indexed owner columns and touches no transcript, which is what the rows themselves cost.
/// </para>
/// </summary>
/// <param name="Conversations">The rows, most recently active first, at most the limit asked for.</param>
/// <param name="Total">Every conversation this caller holds on this platform, however many were
/// served. It is the owner's whole count, so a page can say "20 of 63" without asking again.</param>
public sealed record SpecDialogConversationPage(
    IReadOnlyList<SpecDialogSessionSummary> Conversations, int Total);
