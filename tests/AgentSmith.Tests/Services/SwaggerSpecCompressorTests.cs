using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147c: <see cref="SwaggerSpecCompressor"/> spec-shrinker. Threshold-gated;
/// pass-through under 100k chars; over the threshold strips examples / truncates
/// descriptions / drops unused $ref schemas.
/// </summary>
public sealed class SwaggerSpecCompressorTests
{
    private static ISwaggerSpecCompressor NewSut() =>
        new SwaggerSpecCompressor(NullLogger<SwaggerSpecCompressor>.Instance);

    [Fact]
    public void Compress_UnderThreshold_ReturnsInputUnchanged()
    {
        var spec = MakeSpec(rawJson: """{"openapi":"3.0.1","info":{"title":"Small","version":"v1"},"paths":{}}""");

        var result = NewSut().Compress(spec);

        result.Should().BeSameAs(spec);
    }

    [Fact]
    public void Compress_OverThreshold_KeepsPathsAndMethodsIntact()
    {
        var spec = MakeSpec(rawJson: BuildOversizedSpec());

        var result = NewSut().Compress(spec);

        result.Should().NotBeSameAs(spec);
        result.RawJson.Length.Should().BeLessThan(spec.RawJson.Length);

        using var doc = JsonDocument.Parse(result.RawJson);
        var paths = doc.RootElement.GetProperty("paths");
        // Same path / method shape as the input — only payload shrinks.
        paths.GetProperty("/api/users").GetProperty("get").Should().NotBeNull();
        paths.GetProperty("/api/users").GetProperty("post").Should().NotBeNull();
        paths.GetProperty("/api/orders").GetProperty("get").Should().NotBeNull();
    }

    [Fact]
    public void Compress_OverThreshold_StripsExamplesAndTruncatesDescriptions()
    {
        var spec = MakeSpec(rawJson: BuildOversizedSpec());

        var result = NewSut().Compress(spec);

        result.RawJson.Should().NotContain("\"example\"");
        result.RawJson.Should().NotContain("\"examples\"");
        result.RawJson.Should().NotContain("EXAMPLE_TOKEN_THAT_MUST_NOT_SURVIVE");

        using var doc = JsonDocument.Parse(result.RawJson);
        // Long description got truncated; the trailing ellipsis marker is the contract.
        var users = doc.RootElement.GetProperty("paths").GetProperty("/api/users").GetProperty("get");
        users.GetProperty("description").GetString().Should().EndWith("…");
    }

    [Fact]
    public void Compress_OverThreshold_DropsUnreferencedComponentSchemas()
    {
        var spec = MakeSpec(rawJson: BuildOversizedSpec());

        var result = NewSut().Compress(spec);

        using var doc = JsonDocument.Parse(result.RawJson);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

        // UserResponse is reachable from paths (200 response) → retained.
        schemas.TryGetProperty("UserResponse", out _).Should().BeTrue();
        // OrphanModel is not referenced anywhere → dropped.
        schemas.TryGetProperty("OrphanModel", out _).Should().BeFalse();
    }

    [Fact]
    public void Compress_InvalidJsonOverThreshold_FallsBackToInputUnchanged()
    {
        var junk = new string('x', 200_000); // > threshold, but not parseable JSON
        var spec = MakeSpec(rawJson: junk);

        var result = NewSut().Compress(spec);

        result.Should().BeSameAs(spec);
    }

    private static SwaggerSpec MakeSpec(string rawJson) =>
        new("Test", "v1", Array.Empty<ApiEndpoint>(), Array.Empty<SecurityScheme>(), rawJson);

    /// <summary>
    /// Builds an OpenAPI doc just over the 100k threshold so the compressor engages.
    /// Includes long descriptions, examples, and an unreferenced schema we can assert gets pruned.
    /// </summary>
    private static string BuildOversizedSpec()
    {
        var longDescription = new string('D', 600); // > 240 char description ceiling
        var bigExample = new string('E', 120_000); // pushes RawJson past 100k threshold

        return $$"""
            {
              "openapi": "3.0.1",
              "info": { "title": "Big API", "version": "v1" },
              "paths": {
                "/api/users": {
                  "get": {
                    "description": "{{longDescription}}",
                    "responses": {
                      "200": {
                        "content": {
                          "application/json": {
                            "schema": { "$ref": "#/components/schemas/UserResponse" },
                            "example": "EXAMPLE_TOKEN_THAT_MUST_NOT_SURVIVE_{{bigExample}}"
                          }
                        }
                      }
                    }
                  },
                  "post": {
                    "requestBody": {
                      "content": {
                        "application/json": {
                          "schema": { "$ref": "#/components/schemas/UserResponse" },
                          "examples": { "primary": { "value": "EXAMPLE_TOKEN_THAT_MUST_NOT_SURVIVE" } }
                        }
                      }
                    },
                    "responses": { "201": { "description": "Created" } }
                  }
                },
                "/api/orders": {
                  "get": { "responses": { "200": { "description": "ok" } } }
                }
              },
              "components": {
                "schemas": {
                  "UserResponse": {
                    "type": "object",
                    "properties": {
                      "id": { "type": "integer" },
                      "name": { "type": "string", "example": "Alice EXAMPLE_TOKEN_THAT_MUST_NOT_SURVIVE" }
                    }
                  },
                  "OrphanModel": {
                    "type": "object",
                    "properties": { "unused": { "type": "string" } }
                  }
                }
              }
            }
            """;
    }
}
