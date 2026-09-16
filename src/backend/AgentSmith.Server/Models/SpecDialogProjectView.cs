namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: what a spec dialog scoped to this project is grounded in — the
/// repositories and templates its turns may read. The same shape serves the scope of an
/// OPEN session and the projects a new conversation can be opened on, because the question
/// the operator is asking is the same one: what has the agent actually seen.
/// </summary>
public sealed record SpecDialogProjectView(
    string Name,
    IReadOnlyList<string> Repos,
    IReadOnlyList<SpecDialogTemplateView> Templates);
