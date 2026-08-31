using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: reads the served description the run holds into the operations the
/// difference is computed over — the parameters and body fields each operation ACCEPTS,
/// and the fields a success response RETURNS.
/// <para>
/// The document itself resolves the references its operations use, so an operation whose
/// body is a named component still states its properties. A document that will not parse
/// costs the property comparison, not the operation one: the endpoints are already parsed.
/// </para>
/// </summary>
public sealed class ServedSurfaceReader : IServedSurfaceReader
{
    public IReadOnlyList<ServedOperation> Read(SwaggerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using var document = ParseOrNull(spec.RawJson);
        var refs = SchemaRefResolver.For(document?.RootElement);
        return [.. spec.Endpoints.Select(endpoint => Operation(endpoint, refs))];
    }

    private static ServedOperation Operation(ApiEndpoint endpoint, SchemaRefResolver refs) =>
        new(endpoint.Method,
            endpoint.Path,
            endpoint.OperationId,
            Accepted(endpoint, refs),
            SchemaPropertyNames.InSuccessResponses(endpoint.ResponseSchema, refs));

    /// <summary>
    /// What a caller CHOOSES to send: query parameters and request-body fields.
    /// <para>
    /// A path parameter is part of the operation's identity — a client that calls the
    /// operation sends it by construction — and a header or cookie is usually set once by
    /// a transport wrapper rather than at the call site. Comparing either against what a
    /// call site shows would manufacture a difference no reading could ever refute.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> Accepted(ApiEndpoint endpoint, SchemaRefResolver refs) =>
    [
        .. endpoint.Parameters
            .Where(p => string.Equals(p.In, "query", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Concat(SchemaPropertyNames.In(endpoint.RequestBodySchema, refs))
            .Distinct(StringComparer.Ordinal),
    ];

    private static JsonDocument? ParseOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
