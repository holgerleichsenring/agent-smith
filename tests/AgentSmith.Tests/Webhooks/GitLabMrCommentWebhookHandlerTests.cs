using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Webhooks;

public sealed class GitLabMrCommentWebhookHandlerTests
{
    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static GitLabMrCommentWebhookHandler CreateSut() =>
        new(TestCommentIntentParserFactory.Create(),
            TestCommentIntentParserFactory.Context,
            NullLogger<GitLabMrCommentWebhookHandler>.Instance);

    [Fact]
    public void CanHandle_CorrectEventTypes()
    {
        var sut = CreateSut();

        sut.CanHandle("gitlab", "note hook").Should().BeTrue();
        sut.CanHandle("gitlab", "merge_request").Should().BeFalse();
        sut.CanHandle("github", "note hook").Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_FixCommand_ReturnsPipeline()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("fix-bug");
        result.TriggerInput.Should().Contain("mr:org/my-api!15");
    }

    [Fact]
    public async Task HandleAsync_FixWithTicket_PropagatesTicket()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("fix-bug");
        result.TriggerInput.Should().Contain("#99");
        result.TriggerInput.Should().Contain("mr:org/my-api!15");
    }

    [Fact]
    public async Task HandleAsync_SecurityScan_ReturnsSecurityPipeline()
    {
        var sut = CreateSut();
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
        result.Pipeline.Should().Be("security-scan");
        result.TriggerInput.Should().Contain("mr:org/my-api!3");
    }

    [Fact]
    public async Task HandleAsync_Approve_ReturnsDialogueAnswer()
    {
        var sut = CreateSut();
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
        var sut = CreateSut();
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
        var sut = CreateSut();
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
        var sut = CreateSut();
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
