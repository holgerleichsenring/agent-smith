namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Loads and parses an OpenAPI / swagger.json spec from a local file or URL.
/// </summary>
public interface ISwaggerProvider
{
    Task<SwaggerSpec> LoadAsync(string pathOrUrl, CancellationToken cancellationToken);
}

public sealed record SwaggerSpec(
    string Title,
    string Version,
    IReadOnlyList<ApiEndpoint> Endpoints,
    IReadOnlyList<SecurityScheme> SecuritySchemes,
    string RawJson);

public sealed record ApiEndpoint(
    string Method,
    string Path,
    string? OperationId,
    IReadOnlyList<ApiParameter> Parameters,
    bool RequiresAuth,
    string? RequestBodySchema,
    string? ResponseSchema);

public sealed record ApiParameter(
    string Name,
    string In,
    string? Type,
    bool Required);

public sealed record SecurityScheme(
    string Name,
    string Type,
    string? In,
    string? Scheme);
