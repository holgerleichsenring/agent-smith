using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads and parses a swagger.json / OpenAPI spec from a path or URL.
/// </summary>
public sealed class LoadSwaggerHandler(
    ISwaggerProvider swaggerProvider,
    ILogger<LoadSwaggerHandler> logger)
    : ICommandHandler<LoadSwaggerContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadSwaggerContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<string>(ContextKeys.SwaggerPath, out var swaggerPath)
            || string.IsNullOrWhiteSpace(swaggerPath))
        {
            return CommandResult.Fail("No swagger path specified (--swagger)");
        }

        var spec = await swaggerProvider.LoadAsync(swaggerPath, cancellationToken);
        context.Pipeline.Set(ContextKeys.SwaggerSpec, spec);

        logger.LogInformation(
            "Loaded swagger spec: {Title} v{Version} — {Endpoints} endpoints, {Auth} security schemes",
            spec.Title, spec.Version, spec.Endpoints.Count, spec.SecuritySchemes.Count);

        return CommandResult.Ok(
            $"Swagger loaded: {spec.Title} v{spec.Version} ({spec.Endpoints.Count} endpoints)");
    }
}
