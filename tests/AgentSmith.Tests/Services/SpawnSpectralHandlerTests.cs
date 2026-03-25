using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SpawnSpectralHandlerTests
{
    private readonly Mock<ISpectralScanner> _scannerMock = new();
    private readonly SpawnSpectralHandler _sut;

    public SpawnSpectralHandlerTests()
    {
        _sut = new SpawnSpectralHandler(
            _scannerMock.Object,
            NullLogger<SpawnSpectralHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoSwaggerSpec_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var context = new SpawnSpectralContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("swagger");
    }

    [Fact]
    public async Task ExecuteAsync_WithSwaggerSpec_CallsScannerAndStoresResult()
    {
        var pipeline = new PipelineContext();
        var spec = new SwaggerSpec("Test API", "1.0", [], [], """{"openapi":"3.0.0"}""");
        pipeline.Set(ContextKeys.SwaggerSpec, spec);

        var spectralResult = new SpectralResult(
            [new SpectralFinding("owasp:rule1", "Missing auth", "paths./api.get", "error", 10)],
            ErrorCount: 1,
            WarnCount: 0,
            DurationSeconds: 5);

        _scannerMock
            .Setup(s => s.LintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(spectralResult);

        var context = new SpawnSpectralContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 findings");
        pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var stored).Should().BeTrue();
        stored!.Findings.Should().HaveCount(1);
        stored.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRawJson_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        var spec = new SwaggerSpec("Test", "1.0", [], [], "");
        pipeline.Set(ContextKeys.SwaggerSpec, spec);

        var context = new SpawnSpectralContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
