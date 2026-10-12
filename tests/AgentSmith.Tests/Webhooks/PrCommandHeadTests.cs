using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>An issue comment's command gets its pull request's head from the host; nothing else costs a call.</summary>
public sealed class PrCommandHeadTests
{
    private readonly Mock<ISourceProvider> _provider = new();

    private PrCommandHead Head()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        return new PrCommandHead(PrCommentHandlerFixture.Repos("https://github.com/org/my-api"), sources.Object,
            NullLogger<PrCommandHead>.Instance);
    }

    private static PrCommentCommand Command(string body) =>
        new(body, new PrCommentAuthor("https://github.com/org/my-api", "org/my-api", "alice", "alice"), "org/my-api#42", "pr:org/my-api#42", "42");

    [Fact]
    public async Task CompleteAsync_Command_CarriesTheHostsHead()
    {
        _provider.As<IPullRequestHeadReader>().Setup(r => r.HeadAsync("42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestHead("agent-smith/42", true, "https://github.com/org/my-api/pull/42"));

        var command = await Head().CompleteAsync(Command("/agent-smith"), CancellationToken.None);

        command!.HeadRef.Should().Be("agent-smith/42");
        command.SameRepository.Should().BeTrue();
        command.PrUrl.Should().Be("https://github.com/org/my-api/pull/42");
    }

    [Fact]
    public async Task CompleteAsync_OrdinaryComment_NoHostCall()
    {
        var reader = _provider.As<IPullRequestHeadReader>();

        (await Head().CompleteAsync(Command("looks good"), CancellationToken.None))!.HeadRef.Should().BeNull();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteAsync_FailedRead_IsNull()
    {
        _provider.As<IPullRequestHeadReader>().Setup(r => r.HeadAsync("42", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        (await Head().CompleteAsync(Command("/agent-smith"), CancellationToken.None)).Should().BeNull();
    }
}
