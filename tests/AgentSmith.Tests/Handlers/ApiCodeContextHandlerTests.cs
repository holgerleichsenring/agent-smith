using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Security.Code;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class ApiCodeContextHandlerTests
{
    [Fact]
    public async Task NoSource_SetsApiSourceAvailableFalse()
    {
        var handler = new ApiCodeContextHandler(
            new RouteMapper(NullLogger<RouteMapper>.Instance),
            new AuthBootstrapExtractor(NullLogger<AuthBootstrapExtractor>.Instance),
            new UploadHandlerExtractor(NullLogger<UploadHandlerExtractor>.Instance),
            NullLogger<ApiCodeContextHandler>.Instance);

        var pipeline = new PipelineContext();
        var ctx = new ApiCodeContextCommandContext(pipeline);

        var result = await handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var avail);
        avail.Should().BeFalse();
        pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out _).Should().BeFalse();
    }

    [Fact]
    public async Task WithSource_BuildsApiCodeContext()
    {
        var temp = Path.Combine(Path.GetTempPath(), "api-ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "Ctrl.cs"),
                "[HttpGet(\"/api/users/{id}\")] public IActionResult Get(int id) { }");
            var handler = new ApiCodeContextHandler(
                new RouteMapper(NullLogger<RouteMapper>.Instance),
                new AuthBootstrapExtractor(NullLogger<AuthBootstrapExtractor>.Instance),
                new UploadHandlerExtractor(NullLogger<UploadHandlerExtractor>.Instance),
                NullLogger<ApiCodeContextHandler>.Instance);

            var pipeline = new PipelineContext();
            pipeline.Set(ContextKeys.SourcePath, temp);
            pipeline.Set(ContextKeys.SwaggerSpec, new SwaggerSpec(
                "T", "1.0",
                [new ApiEndpoint("GET", "/api/users/{id}", null, [], true, null, null)],
                [], "{}"));

            var result = await handler.ExecuteAsync(new ApiCodeContextCommandContext(pipeline), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var avail);
            avail.Should().BeTrue();
            pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out var code);
            code!.RoutesToHandlers.Should().HaveCount(1);
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }
}
