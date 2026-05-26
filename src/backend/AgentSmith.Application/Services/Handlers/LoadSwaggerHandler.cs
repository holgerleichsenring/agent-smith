using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads and parses a swagger.json / OpenAPI spec from a path or URL.
/// p0147c: runs the parsed spec through <see cref="ISwaggerSpecCompressor"/> so
/// large specs (e.g. Sample's 291k-char one) don't crowd out the rest of the
/// LLM input window. The compressed shape lands in <see cref="ContextKeys.SwaggerSpec"/>
/// for the default consumer path; the verbatim original stays available under
/// <see cref="ContextKeys.SwaggerSpecFull"/> for skills that need full schema detail.
/// </summary>
public sealed class LoadSwaggerHandler(
    ISwaggerProvider swaggerProvider,
    ISwaggerSpecCompressor compressor,
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

        var fullSpec = await swaggerProvider.LoadAsync(swaggerPath, cancellationToken);
        var compressedSpec = compressor.Compress(fullSpec);

        context.Pipeline.Set(ContextKeys.SwaggerSpec, compressedSpec);
        context.Pipeline.Set(ContextKeys.SwaggerSpecFull, fullSpec);

        logger.LogInformation(
            "Loaded swagger spec: {Title} v{Version} — {Endpoints} endpoints, {Auth} security schemes (raw {FullChars} chars, default {DefaultChars} chars)",
            fullSpec.Title, fullSpec.Version, fullSpec.Endpoints.Count, fullSpec.SecuritySchemes.Count,
            fullSpec.RawJson.Length, compressedSpec.RawJson.Length);

        return CommandResult.Ok(
            $"Swagger loaded: {fullSpec.Title} v{fullSpec.Version} ({fullSpec.Endpoints.Count} endpoints)");
    }
}
