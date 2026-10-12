using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

public sealed class CommentIntentParserTests
{
    private const string ConfigPath = "config.yml";

    private readonly Mock<IIntentParser> _model = new();
    private readonly CommentIntentParser _sut;

    public CommentIntentParserTests()
    {
        _sut = new CommentIntentParser(_model.Object);
    }

    [Theory]
    [InlineData("/agent-smith fix #123 in my-api", "fix #123 in my-api")]
    [InlineData("/as fix", "fix")]
    [InlineData("/AGENT-SMITH fixe einen Bug", "fixe einen Bug")]
    public void Match_SlashPrefix_IsACommandCarryingItsTail(string body, string tail)
    {
        var match = _sut.Match(body);

        match.Type.Should().Be(CommentIntentType.NewJob);
        match.Tail.Should().Be(tail);
    }

    [Fact]
    public void CommentIntentParser_MultiLineBody_TailKeepsAllLines()
    {
        var match = _sut.Match("/agentsmith fix #99 in core\nSome additional context here.\nAnd a third line.");

        match.Tail.Should().Be("fix #99 in core\nSome additional context here.\nAnd a third line.");
    }

    [Fact]
    public void CommentIntentParser_AgentsmithWithoutHyphen_Matches()
    {
        var match = _sut.Match("/agentsmith rework the parser");

        match.Type.Should().Be(CommentIntentType.NewJob);
        match.Tail.Should().Be("rework the parser");
    }

    [Theory]
    [InlineData("/agent-smith")]
    [InlineData("/agentsmith")]
    [InlineData("/as  ")]
    [InlineData("first remark\n/agent-smith")]
    public void CommentIntentParser_BareCommand_MatchesWithEmptyTail(string body)
    {
        var match = _sut.Match(body);

        match.Type.Should().Be(CommentIntentType.NewJob);
        match.Tail.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/as-is this is fine")]
    [InlineData("/assign @someone")]
    [InlineData("/agentsmithy now")]
    public void CommentIntentParser_AsIsAndAssign_DoNotMatch(string body) =>
        _sut.Match(body).Type.Should().Be(CommentIntentType.Unknown);

    [Fact]
    public void Match_Help_IsStructural() =>
        _sut.Match("/agent-smith help").Type.Should().Be(CommentIntentType.Help);

    [Theory]
    [InlineData("")]
    [InlineData("random text")]
    [InlineData("/approve looks good")]
    [InlineData("/reject")]
    public void Match_NoCommandPrefix_IsNoCommand(string body) =>
        _sut.Match(body).Type.Should().Be(CommentIntentType.Unknown);

    [Fact]
    public void Match_NeverAsksTheModel()
    {
        _sut.Match("/agent-smith fix");

        _model.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_HandsTheTailToTheModel()
    {
        var expected = new PipelineRequest("todo-list", "security-scan", Headless: true);
        _model.Setup(p => p.ParseToPipelineRequestAsync("security review", ConfigPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var request = await _sut.ResolveAsync("security review", ConfigPath, CancellationToken.None);

        request.Should().Be(expected);
    }
}
