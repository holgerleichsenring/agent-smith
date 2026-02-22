using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class CheckoutSourceHandlerTests
{
    private readonly Mock<ISourceProviderFactory> _factoryMock = new();
    private readonly CheckoutSourceHandler _handler;

    public CheckoutSourceHandlerTests()
    {
        _handler = new CheckoutSourceHandler(
            _factoryMock.Object,
            NullLoggerFactory.Instance.CreateLogger<CheckoutSourceHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresRepositoryInPipeline()
    {
        var branch = new BranchName("feature/test");
        var repo = new Repository("/tmp/repo", branch, "https://github.com/org/repo.git");
        var providerMock = new Mock<ISourceProvider>();
        providerMock.Setup(p => p.CheckoutAsync(
                It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repo);
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = new CheckoutSourceContext(
            new SourceConfig { Type = "local", Path = "/tmp" }, branch, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<Repository>(ContextKeys.Repository).Should().Be(repo);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<ISourceProvider>();
        providerMock.Setup(p => p.CheckoutAsync(
                It.IsAny<BranchName>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Clone failed"));
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = new CheckoutSourceContext(
            new SourceConfig { Type = "github" }, new BranchName("feature/test"), pipeline);

        var act = async () => await _handler.ExecuteAsync(context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Clone failed");
    }
}
