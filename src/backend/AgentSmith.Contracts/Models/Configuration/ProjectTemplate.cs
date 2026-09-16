namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-09-13-5fa0: a template one context of this project is built after — a context of
/// ANOTHER project, at a revision, with that project's repository already resolved.
/// <para>
/// The binding is per CONTEXT because a component has a template and a repository does
/// not: one repository may hold a server and a client, built after different templates.
/// </para>
/// <para>
/// <see cref="Revision"/> is opaque here and verified where it is fetched
/// (2026-09-13-9802) — every config rule in the store is synchronous and does no I/O, and
/// a revision verified at write time can be deleted five minutes later anyway.
/// </para>
/// </summary>
/// <param name="ContextRepo">2026-09-16-4df5: which repository of THIS project
/// <paramref name="Context"/> belongs to, when two of them declare that name and each wants
/// its own template. Null means "wherever that name is", which is every declaration written
/// before the field existed.</param>
public sealed record ProjectTemplate(
    string Context,
    string TemplateContext,
    string? Revision,
    RepoConnection Repo,
    string? ContextRepo = null);
