namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0315b: a lazy, READ-ONLY sandbox over one repo of a spec-dialog scope.
/// Nothing is spawned until the first step arrives; the underlying container
/// is created on demand (generic git-bearing image, no toolchain build) and
/// the repo is cloned once. Disposal tears the materialised sandbox down; a
/// sandbox that never served a step disposes to nothing.
/// <para>
/// 2026-09-22-46ef: what READ-ONLY means here is a rule about damage, not about
/// step kinds. The four content reads (ReadFile / ListFiles / Grep /
/// DirectoryTree) are served. A process step is served when its program is one
/// the server itself builds — the clone, the file search, the HTTP transfer —
/// and refused otherwise, so the shell a model-authored command travels in
/// never runs and nothing can redirect into the tree. A write is served only
/// when its canonical path lies under a prefix the scope declares, and no
/// prefix is declared by default, so a source scope is writable nowhere.
/// Refusals come back as failed step results, never as exceptions.
/// </para>
/// </summary>
public interface ISourceScopeSandbox : ISandbox
{
    /// <summary>The repo this sandbox grounds (the tool-host address name).</summary>
    string RepoName { get; }

    /// <summary>True once the underlying sandbox has been spawned + cloned.</summary>
    bool IsMaterialized { get; }

    /// <summary>
    /// 2026-09-13-9802: the preparation a read triggers, made explicit so a caller that must
    /// REFUSE learns the KIND before it spends anything — the typed
    /// <see cref="SourceScopeUnavailableException"/> escapes here instead of being flattened
    /// into a step-refusal sentence. Returns the sha the scope landed on.
    /// <para>
    /// 2026-09-13-6f35: on the interface because the coding master opens its templates BEFORE
    /// its first token. A scope it cannot materialise must fail the phase then, not answer
    /// every read with a refusal the model spends a pass discovering.
    /// </para>
    /// </summary>
    Task<string> MaterializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 2026-09-13-9802: the commit this scope is actually on, once it has materialised —
    /// read from the clone, never echoed back from what was asked for. Null before the
    /// first read, and on a scope that asked for no revision and never looked.
    /// </summary>
    string? ResolvedSha { get; }
}
