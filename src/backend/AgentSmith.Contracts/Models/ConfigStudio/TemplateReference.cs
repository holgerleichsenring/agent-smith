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
    string? Revision = null)
{
    public TemplateReference() : this(string.Empty, string.Empty, string.Empty, string.Empty) { }
}
