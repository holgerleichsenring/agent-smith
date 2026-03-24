using AgentSmith.Infrastructure.Services.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SwaggerProviderTests
{
    [Fact]
    public void ParseSpec_OpenApi30_ExtractsEndpointsAndAuth()
    {
        var json = """
            {
              "openapi": "3.0.0",
              "info": { "title": "Pet Store", "version": "1.0.0" },
              "paths": {
                "/pets": {
                  "get": {
                    "operationId": "listPets",
                    "parameters": [
                      { "name": "limit", "in": "query", "schema": { "type": "integer" }, "required": false }
                    ],
                    "security": [{ "bearerAuth": [] }]
                  },
                  "post": {
                    "operationId": "createPet",
                    "requestBody": { "content": { "application/json": {} } }
                  }
                }
              },
              "components": {
                "securitySchemes": {
                  "bearerAuth": { "type": "http", "scheme": "bearer" }
                }
              }
            }
            """;

        var spec = SwaggerProvider.ParseSpec(json);

        spec.Title.Should().Be("Pet Store");
        spec.Version.Should().Be("1.0.0");
        spec.Endpoints.Should().HaveCount(2);
        spec.Endpoints[0].Method.Should().Be("GET");
        spec.Endpoints[0].Path.Should().Be("/pets");
        spec.Endpoints[0].OperationId.Should().Be("listPets");
        spec.Endpoints[0].RequiresAuth.Should().BeTrue();
        spec.Endpoints[0].Parameters.Should().HaveCount(1);
        spec.Endpoints[0].Parameters[0].Name.Should().Be("limit");
        spec.Endpoints[1].Method.Should().Be("POST");
        spec.Endpoints[1].RequiresAuth.Should().BeFalse();
        spec.SecuritySchemes.Should().HaveCount(1);
        spec.SecuritySchemes[0].Name.Should().Be("bearerAuth");
        spec.SecuritySchemes[0].Type.Should().Be("http");
        spec.SecuritySchemes[0].Scheme.Should().Be("bearer");
    }

    [Fact]
    public void ParseSpec_NoPathsOrAuth_ReturnsEmptyLists()
    {
        var json = """
            {
              "openapi": "3.0.0",
              "info": { "title": "Empty API", "version": "0.1.0" }
            }
            """;

        var spec = SwaggerProvider.ParseSpec(json);

        spec.Title.Should().Be("Empty API");
        spec.Endpoints.Should().BeEmpty();
        spec.SecuritySchemes.Should().BeEmpty();
    }

    [Fact]
    public void ParseSpec_Swagger20_ExtractsSecurityDefinitions()
    {
        var json = """
            {
              "swagger": "2.0",
              "info": { "title": "Legacy API", "version": "2.0.0" },
              "paths": {
                "/users": {
                  "get": { "operationId": "getUsers" }
                }
              },
              "securityDefinitions": {
                "apiKey": { "type": "apiKey", "in": "header" }
              }
            }
            """;

        var spec = SwaggerProvider.ParseSpec(json);

        spec.Endpoints.Should().HaveCount(1);
        spec.SecuritySchemes.Should().HaveCount(1);
        spec.SecuritySchemes[0].Name.Should().Be("apiKey");
        spec.SecuritySchemes[0].In.Should().Be("header");
    }
}
