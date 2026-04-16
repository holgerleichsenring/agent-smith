using AgentSmith.Cli.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);

        sut.CanHandle("gitlab", "note hook").Should().BeTrue();
        sut.CanHandle("gitlab", "merge_request").Should().BeFalse();
        sut.CanHandle("github", "note hook").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_FixCommand_ReturnsPipeline()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 200,
                "note": "/agent-smith fix",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug mr:org/my-api!15");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_FixWithArguments_ReturnsArguments()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 201,
                "note": "/agent-smith fix #99 in core",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("fix-bug #99 in core");
        result.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public async Task HandleAsync_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 202,
                "note": "/agent-smith security-scan",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 3 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.TriggerInput.Should().Be("security-scan mr:org/my-api!3");
        result.Pipeline.Should().Be("security-scan");
    }

    [Fact]
    public async Task HandleAsync_Approve_ReturnsDialogueAnswer()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 203,
                "note": "/approve looks good",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeTrue();
        result.DialogueAnswer.Should().NotBeNull();
        result.DialogueAnswer!.Platform.Should().Be("gitlab");
        result.DialogueAnswer.Answer.Should().Be("yes");
        result.DialogueAnswer.PrIdentifier.Should().Be("15");
    }

    [Fact]
    public async Task HandleAsync_NoteOnIssue_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 204,
                "note": "/agent-smith fix",
                "noteable_type": "Issue"
            }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_Help_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 205,
                "note": "/agent-smith help",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_RegularComment_ReturnsNotHandled()
    {
        var sut = new GitLabMrCommentWebhookHandler(
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);
        var payload = """
        {
            "object_kind": "note",
            "user": { "username": "dev-user" },
            "project": { "path_with_namespace": "org/my-api" },
            "object_attributes": {
                "id": 206,
                "note": "Just a regular comment",
                "noteable_type": "MergeRequest"
            },
            "merge_request": { "iid": 15 }
        }
        """;

        var result = await sut.HandleAsync(payload, EmptyHeaders);

        result.Handled.Should().BeFalse();
    }
}
