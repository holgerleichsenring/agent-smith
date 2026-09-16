namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-6d9c: the fix-bug ticket a bug outcome would file — its title, and the body
/// composed exactly as the filer composes it.
/// </summary>
public sealed record SpecDialogBugView(string Title, string Body);
