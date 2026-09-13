namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-13-9802: why a read-only source scope could not be prepared.
/// <para>
/// Four of these used to arrive as one sentence — "could not be prepared: &lt;git stderr&gt;" —
/// and an operator had to read git's wording to tell a typo apart from a missing token.
/// A consumer that must refuse a run needs them separated at the sandbox, because the
/// actions they call for are different: widen a token, fix a revision, wait for a host.
/// </para>
/// </summary>
public enum SourceScopeFailureKind
{
    /// <summary>The connection names no clone url, so nothing can be fetched at all.</summary>
    NoCloneUrl,

    /// <summary>The host refused the credential. One token per platform is all the product has.</summary>
    Unauthorised,

    /// <summary>The host could not be reached — DNS, timeout, transport.</summary>
    Unreachable,

    /// <summary>
    /// The revision does not resolve, even after asking the host for it by name. A typo,
    /// or a ref that no longer exists.
    /// </summary>
    NoSuchRevision,

    /// <summary>
    /// The clone did not carry the revision and the host refused to hand it over on
    /// request. A full clone fetches every branch and tag, so this is the sha that lives
    /// only in a pull-request ref or on a deleted branch — it may well exist, and it is
    /// still not reachable from here.
    /// </summary>
    RevisionNotFetched,
}

/// <summary>
/// 2026-09-13-9802: a read-only source scope that could not be prepared, carrying the
/// repository and the revision that were asked for so a caller can name both.
/// </summary>
public sealed class SourceScopeUnavailableException(
    SourceScopeFailureKind kind, string repoName, string? revision, string message)
    : Exception(message)
{
    public SourceScopeFailureKind Kind { get; } = kind;
    public string RepoName { get; } = repoName;
    public string? Revision { get; } = revision;
}
