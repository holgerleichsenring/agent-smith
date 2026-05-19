using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class CommentIntentParserTests
{
    private const string ConfigPath = "config.yml";

    private readonly Mock<IIntentParser> _intentParserMock = new();
    private readonly CommentIntentParser _sut;

    public CommentIntentParserTests()
    {
        _sut = new CommentIntentParser(_intentParserMock.Object);
    }

    private void SetupLlmIntent(
        string expectedInput, string pipeline, string project = "todo-list", string? ticket = null)
    {
        _intentParserMock
            .Setup(p => p.ParseToPipelineRequestAsync(
                It.Is<string>(s => s == expectedInput),
                ConfigPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRequest(
                project, pipeline,
                TicketId: ticket is null ? null : new TicketId(ticket),
                Headless: true));
    }

    // p0146e: the slash-prefix regex is structural — these tests confirm we still
    // recognise /agent-smith and /as, and delegate the tail to IIntentParser.

    [Fact]
    public async Task AgentSmithPrefix_NewJob_DelegatesTailToIntentParser()
    {
        SetupLlmIntent("fix #123 in my-api", "fix-bug", "my-api", "123");

        var result = await _sut.ParseAsync(
            "/agent-smith fix #123 in my-api", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.NewJob);
        result.Request!.PipelineName.Should().Be("fix-bug");
        result.Request.TicketId!.Value.Should().Be("123");
    }

    [Fact]
    public async Task AsShortPrefix_NewJob_DelegatesTailToIntentParser()
    {
        SetupLlmIntent("fix", "fix-bug");

        var result = await _sut.ParseAsync(
            "/as fix", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.NewJob);
        result.Request!.PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task GermanFreeText_ResolvesViaLlmIntentParser()
    {
        // The post-slash body can be free-form text in any language — the LLM
        // resolves "fixe einen Bug" → fix-bug. This is the headline win of p0146e
        // (no more "German/English trap" from the deleted PipelineAliases table).
        SetupLlmIntent("fixe einen Bug", "fix-bug");

        var result = await _sut.ParseAsync(
            "/agent-smith fixe einen Bug", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.NewJob);
        result.Request!.PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task SecurityReview_ResolvesToSecurityScan_ViaLlmIntentParser()
    {
        // Replaces the deleted "security" → "security-scan" alias entry in the old
        // PipelineAliases table.
        SetupLlmIntent("security review", "security-scan");

        var result = await _sut.ParseAsync(
            "/agent-smith security review", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.NewJob);
        result.Request!.PipelineName.Should().Be("security-scan");
    }

    [Fact]
    public async Task Help_ReturnsHelp_WithoutLlmCall()
    {
        var result = await _sut.ParseAsync(
            "/agent-smith help", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.Help);
        result.Request.Should().BeNull();
        _intentParserMock.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MultiLine_CommandOnFirstLine_DelegatesFirstLineTailToIntentParser()
    {
        SetupLlmIntent("fix #99 in core", "fix-bug", "core", "99");

        var body = """
            /agent-smith fix #99 in core
            Some additional context here.
            More details on what to fix.
            """;

        var result = await _sut.ParseAsync(body, ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.NewJob);
        result.Request!.PipelineName.Should().Be("fix-bug");
    }

    // /approve and /reject paths stay structural — no LLM call, body text passes
    // through unchanged as DialogueComment.

    [Fact]
    public async Task Approve_WithoutComment_ReturnsDialogueApprove_NoLlmCall()
    {
        var result = await _sut.ParseAsync("/approve", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.DialogueApprove);
        result.DialogueComment.Should().BeNull();
        _intentParserMock.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_WithComment_ReturnsDialogueApprove_WithComment()
    {
        var result = await _sut.ParseAsync(
            "/approve LGTM Please rename branch", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.DialogueApprove);
        result.DialogueComment.Should().Be("LGTM Please rename branch");
    }

    [Fact]
    public async Task Approve_CaseInsensitive_ReturnsDialogueApprove()
    {
        var result = await _sut.ParseAsync("/APPROVE", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.DialogueApprove);
    }

    [Fact]
    public async Task Reject_WithoutComment_ReturnsDialogueReject_NoLlmCall()
    {
        var result = await _sut.ParseAsync("/reject", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.DialogueReject);
        result.DialogueComment.Should().BeNull();
        _intentParserMock.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reject_WithComment_ReturnsDialogueReject_WithComment()
    {
        var result = await _sut.ParseAsync(
            "/reject typo The naming is wrong", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.DialogueReject);
        result.DialogueComment.Should().Be("typo The naming is wrong");
    }

    // Non-slash-prefix bodies are not commands — no LLM call, return Unknown.

    [Fact]
    public async Task RandomText_ReturnsUnknown_NoLlmCall()
    {
        var result = await _sut.ParseAsync("random text", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.Unknown);
        _intentParserMock.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyString_ReturnsUnknown_NoLlmCall()
    {
        var result = await _sut.ParseAsync("", ConfigPath, CancellationToken.None);

        result.Type.Should().Be(CommentIntentType.Unknown);
        _intentParserMock.Verify(p => p.ParseToPipelineRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
