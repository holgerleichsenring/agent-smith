using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks;

public sealed class CommentIntentParserTests
{
    [Fact]
    public void Fix_ReturnNewJob_WithFixBugPipeline()
    {
        var result = CommentIntentParser.Parse("/agent-smith fix", out var pipeline, out var args, out var comment);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("fix-bug");
        args.Should().BeNull();
        comment.Should().BeNull();
    }

    [Fact]
    public void Fix_WithArguments_ReturnNewJob_WithArguments()
    {
        var result = CommentIntentParser.Parse("/agent-smith fix #123 in my-api", out var pipeline, out var args, out var comment);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("fix-bug");
        args.Should().Be("#123 in my-api");
        comment.Should().BeNull();
    }

    [Fact]
    public void ShortForm_Fix_ReturnNewJob()
    {
        var result = CommentIntentParser.Parse("/as fix", out var pipeline, out var args, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("fix-bug");
        args.Should().BeNull();
    }

    [Fact]
    public void SecurityScan_ReturnNewJob_WithSecurityScanPipeline()
    {
        var result = CommentIntentParser.Parse("/agent-smith security-scan", out var pipeline, out _, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("security-scan");
    }

    [Fact]
    public void SecurityAlias_ReturnNewJob_WithSecurityScanPipeline()
    {
        var result = CommentIntentParser.Parse("/agent-smith security", out var pipeline, out _, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("security-scan");
    }

    [Fact]
    public void Review_ReturnNewJob_WithPrReviewPipeline()
    {
        var result = CommentIntentParser.Parse("/agent-smith review", out var pipeline, out _, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("pr-review");
    }

    [Fact]
    public void Help_ReturnHelp()
    {
        var result = CommentIntentParser.Parse("/agent-smith help", out var pipeline, out var args, out _);

        result.Should().Be(CommentIntentType.Help);
        pipeline.Should().BeNull();
        args.Should().BeNull();
    }

    [Fact]
    public void UnknownCommand_ReturnNewJob_WithCommandAsPipeline()
    {
        var result = CommentIntentParser.Parse("/agent-smith unknown-cmd", out var pipeline, out _, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("unknown-cmd");
    }

    [Fact]
    public void Approve_WithoutComment_ReturnDialogueApprove()
    {
        var result = CommentIntentParser.Parse("/approve", out var pipeline, out var args, out var comment);

        result.Should().Be(CommentIntentType.DialogueApprove);
        pipeline.Should().BeNull();
        args.Should().BeNull();
        comment.Should().BeNull();
    }

    [Fact]
    public void Approve_WithComment_ReturnDialogueApprove_WithComment()
    {
        var result = CommentIntentParser.Parse("/approve Please rename branch", out _, out _, out var comment);

        result.Should().Be(CommentIntentType.DialogueApprove);
        comment.Should().Be("Please rename branch");
    }

    [Fact]
    public void Approve_CaseInsensitive_ReturnDialogueApprove()
    {
        var result = CommentIntentParser.Parse("/APPROVE", out _, out _, out _);

        result.Should().Be(CommentIntentType.DialogueApprove);
    }

    [Fact]
    public void Reject_WithoutComment_ReturnDialogueReject()
    {
        var result = CommentIntentParser.Parse("/reject", out var pipeline, out var args, out var comment);

        result.Should().Be(CommentIntentType.DialogueReject);
        pipeline.Should().BeNull();
        args.Should().BeNull();
        comment.Should().BeNull();
    }

    [Fact]
    public void Reject_WithComment_ReturnDialogueReject_WithComment()
    {
        var result = CommentIntentParser.Parse("/reject The naming is wrong", out _, out _, out var comment);

        result.Should().Be(CommentIntentType.DialogueReject);
        comment.Should().Be("The naming is wrong");
    }

    [Fact]
    public void RandomText_ReturnUnknown()
    {
        var result = CommentIntentParser.Parse("random text", out var pipeline, out var args, out _);

        result.Should().Be(CommentIntentType.Unknown);
        pipeline.Should().BeNull();
        args.Should().BeNull();
    }

    [Fact]
    public void EmptyString_ReturnUnknown()
    {
        var result = CommentIntentParser.Parse("", out var pipeline, out var args, out _);

        result.Should().Be(CommentIntentType.Unknown);
        pipeline.Should().BeNull();
        args.Should().BeNull();
    }

    [Fact]
    public void MultiLine_CommandOnFirstLine_ShouldMatch()
    {
        var body = """
            /agent-smith fix #99 in core
            Some additional context here.
            More details on what to fix.
            """;

        var result = CommentIntentParser.Parse(body, out var pipeline, out var args, out _);

        result.Should().Be(CommentIntentType.NewJob);
        pipeline.Should().Be("fix-bug");
        args.Should().Be("#99 in core");
    }
}
