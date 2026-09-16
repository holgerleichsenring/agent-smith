namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-09-13-5fa0: the editable studio view of one template binding — a context of THIS
/// project built after (project, repo, context) of another, at a revision.
/// <para>
/// It round-trips through YAML and the CLI and has no studio picker: contexts are
/// discovered inside a run and no endpoint serves them, so a typed picker has no data.
/// This is a dead form until 2026-09-13-84c0 consumes it, and that phase is named.
/// </para>
/// </summary>
public sealed record TemplateReference(
    string Context,
    string Project,
    string Repo,
    string TemplateContext,
    string? Revision = null,
    // 2026-09-16-4df5: which repository of THIS project the local context belongs to.
    // Optional, and absent is what every stored declaration says today: "the context of that
    // name, wherever it is" — correct for any project whose repositories do not collide.
    // Named only when two of them declare one name and each wants its own template.
    string? ContextRepo = null)
{
    public TemplateReference() : this(string.Empty, string.Empty, string.Empty, string.Empty) { }
}
