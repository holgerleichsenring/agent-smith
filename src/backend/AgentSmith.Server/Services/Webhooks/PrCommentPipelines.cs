using AgentSmith.Contracts.Commands;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// The presets a pull-request / merge-request COMMENT may start, for every host alike.
/// <para>
/// 2026-09-25-e5b1: one list, because there were three identical ones — GitHub, GitLab and
/// Azure DevOps each held their own copy naming the alias <c>fix-bug</c>. Deleting that alias
/// without adding the preset it resolved to would have made the code pipeline unreachable
/// from a comment on all three hosts: a capability lost while the diff only looked like a
/// rename. A single list is the shape in which that cannot happen once.
/// </para>
/// <para>
/// It is deliberately NOT <see cref="PipelinePresets.Routable"/>: a comment is a lower trust
/// surface than an operator's configuration, so what it may start is enumerated here rather
/// than inherited from everything the product offers.
/// </para>
/// </summary>
internal static class PrCommentPipelines
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PipelinePresets.CodeName,
        "security-scan",
        "pr-review",
    };
}
