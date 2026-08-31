namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0285: raw YAML shape of one <c>project.repos[]</c> item. Accepts either a scalar string
/// (catalog name, or <c>connection/name</c>, or <c>connection/glob</c>) or a mapping
/// <c>{repo: &lt;ref&gt;, default_branch: &lt;branch&gt;, consumes: &lt;interface&gt;}</c> that adds a
/// per-repo default-branch override and, 2026-08-30-c6ec, the served interface this repo
/// consumes. Both only apply to exact (wildcard-free) refs.
/// </summary>
public sealed record RawRepoRef(string Ref, string? DefaultBranch = null, string? Consumes = null);
