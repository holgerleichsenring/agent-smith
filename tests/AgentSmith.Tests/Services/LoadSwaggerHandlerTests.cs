using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147c: LoadSwaggerHandler runs the fetched SwaggerSpec through ISwaggerSpecCompressor;
/// the compressed shape lands in <see cref="ContextKeys.SwaggerSpec"/>; the original always
/// lands in <see cref="ContextKeys.SwaggerSpecFull"/> for skills that need full schema detail.
/// </summary>
public sealed class LoadSwaggerHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesBothSwaggerSpecAndSwaggerSpecFull()
    {
        var loaded = MakeSpec("raw");
        var compressed = MakeSpec("compressed");
        var sut = MakeHandler(loaded, compressed);

        var context = NewContext(swaggerPath: "/tmp/swagger.json");
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var stored).Should().BeTrue();
        stored.Should().BeSameAs(compressed);

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpecFull, out var full).Should().BeTrue();
        full.Should().BeSameAs(loaded);
    }

    [Fact]
    public async Task ExecuteAsync_CompressorPassThrough_PublishesSameInstanceInBothSlots()
    {
        var loaded = MakeSpec("raw");
        // Pass-through compressor returns the input unchanged for small specs.
        var sut = MakeHandler(loaded, compressed: loaded);

        var context = NewContext(swaggerPath: "/tmp/swagger.json");
        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var stored);
        context.Pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpecFull, out var full);

        stored.Should().BeSameAs(full); // same SwaggerSpec instance in both slots
    }

    [Fact]
    public async Task ExecuteAsync_MissingSwaggerPath_ReturnsFail()
    {
        var sut = MakeHandler(MakeSpec("raw"), MakeSpec("raw"));

        var context = new LoadSwaggerContext(new PipelineContext());
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static LoadSwaggerHandler MakeHandler(SwaggerSpec loaded, SwaggerSpec compressed)
    {
        var provider = new Mock<ISwaggerProvider>();
        provider.Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loaded);

        var compressor = new Mock<ISwaggerSpecCompressor>();
        compressor.Setup(c => c.Compress(It.IsAny<SwaggerSpec>())).Returns(compressed);

        return new LoadSwaggerHandler(
            provider.Object,
            compressor.Object,
            NullLogger<LoadSwaggerHandler>.Instance);
    }

    private static LoadSwaggerContext NewContext(string swaggerPath)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SwaggerPath, swaggerPath);
        return new LoadSwaggerContext(pipeline);
    }

    private static SwaggerSpec MakeSpec(string raw) =>
        new("T", "v1", Array.Empty<ApiEndpoint>(), Array.Empty<SecurityScheme>(), raw);
}
