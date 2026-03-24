using System.Text.Json;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers;

/// <summary>
/// Loads and parses OpenAPI / swagger.json from a local file or HTTP URL.
/// </summary>
public sealed class SwaggerProvider(
    ILogger<SwaggerProvider> logger) : ISwaggerProvider
{
    private static readonly HttpClient HttpClient = new(
        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true })
    { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<SwaggerSpec> LoadAsync(string pathOrUrl, CancellationToken cancellationToken)
    {
        var json = pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? await HttpClient.GetStringAsync(pathOrUrl, cancellationToken)
            : await File.ReadAllTextAsync(pathOrUrl, cancellationToken);

        logger.LogInformation("Loaded swagger spec ({Chars} chars) from {Source}", json.Length, pathOrUrl);

        return ParseSpec(json);
    }

    internal static SwaggerSpec ParseSpec(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var title = root.TryGetProperty("info", out var info) && info.TryGetProperty("title", out var t)
            ? t.GetString() ?? "Unknown" : "Unknown";
        var version = info.TryGetProperty("version", out var v) ? v.GetString() ?? "0.0.0" : "0.0.0";

        var endpoints = ParseEndpoints(root);
        var securitySchemes = ParseSecuritySchemes(root);

        return new SwaggerSpec(title, version, endpoints, securitySchemes, json);
    }

    private static List<ApiEndpoint> ParseEndpoints(JsonElement root)
    {
        var endpoints = new List<ApiEndpoint>();

        if (!root.TryGetProperty("paths", out var paths))
            return endpoints;

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                if (method.Name.StartsWith("x-")) continue;

                var parameters = ParseParameters(method.Value);
                var operationId = method.Value.TryGetProperty("operationId", out var oid)
                    ? oid.GetString() : null;
                var requiresAuth = method.Value.TryGetProperty("security", out _);
                var requestBody = method.Value.TryGetProperty("requestBody", out var rb)
                    ? rb.ToString() : null;
                var responseSchema = method.Value.TryGetProperty("responses", out var resp)
                    ? resp.ToString() : null;

                endpoints.Add(new ApiEndpoint(
                    method.Name.ToUpperInvariant(), path.Name, operationId,
                    parameters, requiresAuth, requestBody, responseSchema));
            }
        }

        return endpoints;
    }

    private static List<ApiParameter> ParseParameters(JsonElement operation)
    {
        var parameters = new List<ApiParameter>();

        if (!operation.TryGetProperty("parameters", out var paramsArray))
            return parameters;

        foreach (var param in paramsArray.EnumerateArray())
        {
            var name = param.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var inValue = param.TryGetProperty("in", out var i) ? i.GetString() ?? "query" : "query";
            var type = param.TryGetProperty("schema", out var schema) && schema.TryGetProperty("type", out var st)
                ? st.GetString() : null;
            var required = param.TryGetProperty("required", out var r) && r.GetBoolean();

            parameters.Add(new ApiParameter(name, inValue, type, required));
        }

        return parameters;
    }

    private static List<SecurityScheme> ParseSecuritySchemes(JsonElement root)
    {
        var schemes = new List<SecurityScheme>();

        if (!root.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("securitySchemes", out var secSchemes))
        {
            if (root.TryGetProperty("securityDefinitions", out secSchemes)) { }
            else return schemes;
        }

        foreach (var scheme in secSchemes.EnumerateObject())
        {
            var type = scheme.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var inValue = scheme.Value.TryGetProperty("in", out var i) ? i.GetString() : null;
            var schemeValue = scheme.Value.TryGetProperty("scheme", out var s) ? s.GetString() : null;

            schemes.Add(new SecurityScheme(scheme.Name, type, inValue, schemeValue));
        }

        return schemes;
    }
}
