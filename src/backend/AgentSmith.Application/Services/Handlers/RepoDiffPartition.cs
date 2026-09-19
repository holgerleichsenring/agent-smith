using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0355: the outcome of partitioning a run's repos by working-tree diff —
/// the sandboxes of the repos that actually changed (the only ones the
/// post-execute passes may touch), their repo names in scope order, and the
/// names of the repos skipped because nothing in them changed.
/// <para>
/// 2026-09-19-c511a: and the first changed repo's own sandbox, because the dictionary is keyed by
/// SANDBOX KEY (SandboxKeyComposer: "default", a context slug, or "&lt;repo&gt;-&lt;context&gt;") while
/// the names are REPO names, and the two coincide in only one of the four shapes. A caller that
/// needs the sandbox behind ChangedRepoNames[0] cannot get it by indexing. Null when nothing
/// changed.
/// </para>
/// </summary>
public sealed record RepoDiffPartition(
    IReadOnlyDictionary<string, ISandbox> ChangedSandboxes,
    IReadOnlyList<string> ChangedRepoNames,
    IReadOnlyList<string> SkippedRepoNames,
    ISandbox? FirstChangedSandbox);
