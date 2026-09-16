namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: one template a spec-dialog turn may read, as the surface names it.
/// The revision is what the project DECLARED — nothing has been fetched at the time the
/// surface asks, and a sha would be a claim about a checkout that does not exist yet.
/// </summary>
public sealed record SpecDialogTemplateView(string Name, string Repo, string Revision);
