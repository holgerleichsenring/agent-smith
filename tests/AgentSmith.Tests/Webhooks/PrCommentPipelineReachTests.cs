using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// 2026-09-25-e5b1: the sharpest edge of deleting the alias map. GitHub, GitLab and Azure DevOps
/// each held their own allow-list for what a pull-request comment may start, and every one of them
/// named <c>fix-bug</c>. Removing that entry without adding the preset it resolved to would have
/// made the code pipeline unreachable from a comment on all three hosts — a CAPABILITY lost while
/// the diff read like a rename, and nothing in the suite would have said so.
/// </summary>
public sealed class PrCommentPipelineReachTests
{
    [Fact]
    public void PrComment_TheCodePreset_IsReachableOnAllThreeHosts()
    {
        PrCommentPipelines.Allowed.Should().Contain(PipelinePresets.CodeName);
        Handlers().Should().OnlyContain(
            source => source.Contains("PrCommentPipelines.Allowed", StringComparison.Ordinal),
            "three copies of one list is how the capability nearly went missing — every host "
            + "reads the same one, so 'on all three hosts' is a property of the code, not of "
            + "three assertions that could drift apart");
    }

    [Fact]
    public void PrComment_TheAllowList_NamesNoRetiredNameAndNoSurprise()
    {
        // A comment is a lower-trust surface than an operator's configuration, so the list is
        // enumerated rather than inherited from everything the product offers — this pins that
        // it did not quietly widen while it was being moved.
        PrCommentPipelines.Allowed.Should().BeEquivalentTo(
            [PipelinePresets.CodeName, "security-scan", "pr-review"]);
        PrCommentPipelines.Allowed.Should().OnlyContain(name => PipelinePresets.TryResolve(name) != null);
    }

    private static IEnumerable<string> Handlers() =>
        new[]
        {
            "GitHubPrCommentWebhookHandler", "GitLabMrCommentWebhookHandler",
            "AzureDevOpsPrCommentWebhookHandler",
        }.Select(name => File.ReadAllText(Path.Combine(
            Architecture.ArchitectureSources.BackendRoot,
            "AgentSmith.Server", "Services", "Webhooks", $"{name}.cs")));
}
