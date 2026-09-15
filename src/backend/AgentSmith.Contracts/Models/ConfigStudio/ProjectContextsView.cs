namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-09-14-620e: the context NAMES one repository of one project declares, as the
/// studio's template form offers them.
/// <para>
/// It keeps the distinction <see cref="Sandbox.RemoteContextListing"/> exists to keep:
/// an empty list with no <paramref name="UnreadableReason"/> means the repository declares
/// nothing, and a reason means it could not be read at all. Collapsing the two would tell
/// an operator to declare a context when the truth is that their credential cannot reach
/// the repository.
/// </para>
/// </summary>
/// <param name="Contexts">The context names, as read from the repository's DEFAULT
/// BRANCH — no provider reads a directory at an arbitrary revision, so a context that
/// exists only at the template's pinned revision is not among them.</param>
/// <param name="UnreadableReason">Why the listing could not happen, stripped of anything
/// a URL can smuggle. Null when the listing succeeded, whatever it contained.</param>
public sealed record ProjectContextsView(
    IReadOnlyList<string> Contexts,
    string? UnreadableReason = null);
