using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0315b: builds lazy read-only <see cref="ISourceScopeSandbox"/> instances
/// for the repos of a spec-dialog scope. Creation is cheap (no container is
/// spawned) — materialisation happens inside the sandbox on first use.
/// </summary>
public interface ISourceScopeSandboxFactory
{
    /// <param name="revision">
    /// 2026-09-13-9802: an optional branch, tag or sha to materialise at. Null means the
    /// clone's own default, which is what every caller before templates wanted. The clone
    /// is full — no depth, no --no-tags — so a named revision is a checkout and not a
    /// second protocol.
    /// </param>
    /// <param name="conversationId">
    /// 2026-09-22-2d11a: the design conversation this scope belongs to, stamped on the
    /// sandbox as a label. Null leaves the label off and the reapers' third rail out of
    /// reach — what a RUN passes. 2026-09-22-2d11b: a design turn names its conversation,
    /// and the scope then takes the sandbox that conversation already holds and hands it
    /// back when the turn ends.
    /// </param>
    ISourceScopeSandbox Create(
        ResolvedProject project, RepoConnection repo, string? revision = null,
        string? conversationId = null);
}
