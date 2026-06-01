using AgentSmith.Contracts.Providers;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>p0196: returns a minimal SwaggerSpec without hitting the network.</summary>
internal sealed class StubSwaggerProvider : ISwaggerProvider
{
    public Task<SwaggerSpec> LoadAsync(string pathOrUrl, CancellationToken cancellationToken) =>
        Task.FromResult(new SwaggerSpec(
            Title: "Stub API",
            Version: "1.0",
            Endpoints: new[]
            {
                new ApiEndpoint("GET", "/health", "getHealth", Array.Empty<ApiParameter>(),
                    RequiresAuth: false, RequestBodySchema: null, ResponseSchema: null),
            },
            SecuritySchemes: Array.Empty<SecurityScheme>(),
            RawJson: """{"openapi":"3.0.0","info":{"title":"Stub","version":"1.0"},"paths":{}}"""));
}
