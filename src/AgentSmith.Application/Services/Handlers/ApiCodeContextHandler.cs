using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds an ApiCodeContext when ContextKeys.SourcePath is set (by the
/// --source-path CLI flag or by TryCheckoutSourceHandler resolving the
/// configured source: block). Walks the source tree once via the route
/// mapper and the two extractors, then exposes the findings under
/// ContextKeys.ApiCodeContext for downstream skills.
/// </summary>
public sealed class ApiCodeContextHandler(
    IRouteMapper routeMapper,
    IAuthBootstrapExtractor authExtractor,
    IUploadHandlerExtractor uploadExtractor,
    ILogger<ApiCodeContextHandler> logger)
    : ICommandHandler<ApiCodeContextCommandContext>
{
    public Task<CommandResult> ExecuteAsync(
        ApiCodeContextCommandContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!TryResolveSourcePath(pipeline, out var sourcePath))
        {
            pipeline.Set(ContextKeys.ApiSourceAvailable, false);
            logger.LogInformation("No source path provided — code-aware analysis disabled");
            return Task.FromResult(CommandResult.Ok("No source path; code-aware analysis disabled"));
        }

        var spec = pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var s) ? s : null;
        var endpoints = spec?.Endpoints ?? Array.Empty<ApiEndpoint>();

        var routes = routeMapper.MapRoutes(endpoints, sourcePath);
        var auth = authExtractor.ExtractAuthBootstrap(sourcePath);
        var middleware = authExtractor.ExtractSecurityMiddleware(sourcePath);
        var uploads = uploadExtractor.ExtractUploadHandlers(sourcePath);
        var confidence = ComputeMappingConfidence(routes, endpoints.Count);

        pipeline.Set(ContextKeys.ApiCodeContext,
            new ApiCodeContext(routes, auth, middleware, uploads, confidence));
        pipeline.Set(ContextKeys.ApiSourceAvailable, true);

        logger.LogInformation(
            "ApiCodeContext: {Routes} routes mapped (conf {Conf:P0}), {Auth} auth blocks, {Mw} middleware, {Up} upload sites",
            routes.Count, confidence, auth.Count, middleware.Count, uploads.Count);
        return Task.FromResult(CommandResult.Ok($"Code context built: {routes.Count} routes mapped"));
    }

    private static bool TryResolveSourcePath(PipelineContext pipeline, out string sourcePath)
    {
        sourcePath = pipeline.TryGet<string>(ContextKeys.SourcePath, out var p) ? p ?? "" : "";
        return !string.IsNullOrWhiteSpace(sourcePath) && Directory.Exists(sourcePath);
    }

    private static double ComputeMappingConfidence(IReadOnlyList<RouteHandlerLocation> routes, int total) =>
        total == 0 ? 0.0 : Math.Min(1.0, routes.Count / (double)total);
}
