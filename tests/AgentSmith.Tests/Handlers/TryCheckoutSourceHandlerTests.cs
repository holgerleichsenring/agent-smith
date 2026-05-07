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
    private readonly Mock<IHostSourceCloner> _clonerMock = new();
    private readonly TryCheckoutSourceHandler _handler;

    public TryCheckoutSourceHandlerTests()
    {
        _handler = new TryCheckoutSourceHandler(
            _clonerMock.Object,
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
            _clonerMock.Verify(
                c => c.TryCloneAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()),
                Times.Never);
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
    public async Task ExecuteAsync_RemoteCloneSucceeds_SetsSourcePathToHostTempdir()
    {
        var hostPath = "/tmp/agentsmith-src-" + Guid.NewGuid().ToString("N");
        _clonerMock.Setup(c => c.TryCloneAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hostPath);

        var pipeline = new PipelineContext();
        var context = new TryCheckoutSourceContext(
            new SourceConfig { Type = "github", Url = "https://github.com/x/y" }, null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.SourcePath).Should().Be(hostPath);
        pipeline.Get<Repository>(ContextKeys.Repository).RemoteUrl.Should().Be("https://github.com/x/y");
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
        _clonerMock.Verify(
            c => c.TryCloneAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RemoteCloneReturnsNull_OkAndSourcePathUnset()
    {
        _clonerMock.Setup(c => c.TryCloneAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

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
