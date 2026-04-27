using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class TryCheckoutSourceHandlerTests
{
    private readonly Mock<ISourceProviderFactory> _factoryMock = new();
    private readonly TryCheckoutSourceHandler _handler;

    public TryCheckoutSourceHandlerTests()
    {
        _handler = new TryCheckoutSourceHandler(
            _factoryMock.Object,
            NullLogger<TryCheckoutSourceHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoSourceConfigured_OkAndSourcePathUnset()
    {
        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(new SourceConfig(), null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SourcePath, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_CliFlagAlreadySetSourcePath_DoesNotOverwrite()
    {
        var temp = CreateTempDir();
        try
        {
            var pipeline = new PipelineContext();
            pipeline.Set(ContextKeys.SourcePath, temp);
            var context = new TryCheckoutSourceContext(
                new SourceConfig { Type = "github", Url = "https://github.com/x/y" }, null, pipeline);

            var result = await _handler.ExecuteAsync(context, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            pipeline.Get<string>(ContextKeys.SourcePath).Should().Be(temp);
            _factoryMock.Verify(f => f.Create(It.IsAny<SourceConfig>()), Times.Never);
        }
        finally { TryDelete(temp); }
    }

    [Fact]
    public async Task ExecuteAsync_LocalConfigPathExists_SetsSourcePathAbsolute()
    {
        var temp = CreateTempDir();
        try
        {
            var pipeline = new PipelineContext();
            var context = new TryCheckoutSourceContext(
                new SourceConfig { Type = "local", Path = temp }, null, pipeline);

            var result = await _handler.ExecuteAsync(context, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            pipeline.Get<string>(ContextKeys.SourcePath).Should().Be(Path.GetFullPath(temp));
        }
        finally { TryDelete(temp); }
    }

    [Fact]
    public async Task ExecuteAsync_LocalConfigPathMissing_OkAndSourcePathUnset()
    {
        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "local", Path = "/does/not/exist/" + Guid.NewGuid() },
            null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SourcePath, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_GitHubConfigWithUrl_DelegatesToProviderAndSetsSourcePathFromLocalPath()
    {
        var branch = new BranchName("main");
        var repo = new Repository("/tmp/cloned-repo", branch, "https://github.com/x/y.git");
        var providerMock = new Mock<ISourceProvider>();
        providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repo);
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "github", Url = "https://github.com/x/y" }, branch, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.SourcePath).Should().Be("/tmp/cloned-repo");
        providerMock.Verify(p => p.CheckoutAsync(branch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RemoteConfigEmptyUrl_OkAndSourcePathUnset()
    {
        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "github", Url = null }, null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SourcePath, out _).Should().BeFalse();
        _factoryMock.Verify(f => f.Create(It.IsAny<SourceConfig>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFactoryThrowsOnMissingToken_LogsWarningAndReturnsOk()
    {
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>()))
            .Throws(new InvalidOperationException("GITHUB_TOKEN not set"));

        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "github", Url = "https://github.com/x/y" }, null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SourcePath, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ProviderCheckoutThrows_LogsWarningAndReturnsOk()
    {
        var providerMock = new Mock<ISourceProvider>();
        providerMock.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network unreachable"));
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>())).Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "gitlab", Url = "https://gitlab.com/x/y" }, null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SourcePath, out _).Should().BeFalse();
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "try-checkout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }
}
