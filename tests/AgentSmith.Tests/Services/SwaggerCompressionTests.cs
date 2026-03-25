using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class SwaggerCompressionTests
{
    private const string SampleSwaggerJson = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Test API", "version": "v1" },
          "paths": {
            "/api/users": {
              "get": {
                "operationId": "GetUsers",
                "parameters": [
                  { "name": "page", "in": "query", "schema": { "type": "integer" } }
                ],
                "responses": {
                  "200": {
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/UserListResponse" }
                      },
                      "text/plain": {
                        "schema": { "$ref": "#/components/schemas/UserListResponse" }
                      },
                      "text/json": {
                        "schema": { "$ref": "#/components/schemas/UserListResponse" }
                      }
                    }
                  }
                }
              },
              "post": {
                "operationId": "CreateUser",
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/CreateUserRequest" }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/UserResponse" }
                      }
                    }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "UserListResponse": {
                "type": "object",
                "properties": {
                  "users": { "type": "array", "items": { "$ref": "#/components/schemas/UserResponse" } },
                  "total": { "type": "integer" }
                }
              },
              "UserResponse": {
                "type": "object",
                "required": ["id", "email"],
                "properties": {
                  "id": { "type": "integer" },
                  "email": { "type": "string", "maxLength": 255 },
                  "passwordHash": { "type": "string", "nullable": true },
                  "role": { "type": "integer", "enum": [0, 100, 110, 120, 130] }
                }
              },
              "CreateUserRequest": {
                "type": "object",
                "properties": {
                  "email": { "type": "string" },
                  "name": { "type": "string" }
                }
              }
            }
          }
        }
        """;

    [Fact]
    public void CompressSwaggerSpec_ReducesSize()
    {
        var spec = new SwaggerSpec("Test API", "v1",
            [
                new ApiEndpoint("GET", "/api/users", "GetUsers",
                    [new ApiParameter("page", "query", "integer", false)],
                    false, null,
                    """{"200":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/UserListResponse"}},"text/plain":{"schema":{"$ref":"#/components/schemas/UserListResponse"}},"text/json":{"schema":{"$ref":"#/components/schemas/UserListResponse"}}}}}"""),
                new ApiEndpoint("POST", "/api/users", "CreateUser", [],
                    false,
                    """{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/CreateUserRequest"}}}}""",
                    """{"201":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/UserResponse"}}}}}""")
            ],
            [], SampleSwaggerJson);

        var result = ApiSkillRoundHandler.CompressSwaggerSpec(spec);

        result.Length.Should().BeLessThan(SampleSwaggerJson.Length);
    }

    [Fact]
    public void CompressSwaggerSpec_ContainsSchemaFields()
    {
        var spec = new SwaggerSpec("Test", "v1", [], [], SampleSwaggerJson);

        var result = ApiSkillRoundHandler.CompressSwaggerSpec(spec);

        result.Should().Contain("UserResponse:");
        result.Should().Contain("email: string");
        result.Should().Contain("passwordHash: string");
        result.Should().Contain("nullable");
        result.Should().Contain("maxLength:255");
        result.Should().Contain("enum[0, 100, 110, 120, 130]");
    }

    [Fact]
    public void CompressSwaggerSpec_MarksRequiredFields()
    {
        var spec = new SwaggerSpec("Test", "v1", [], [], SampleSwaggerJson);

        var result = ApiSkillRoundHandler.CompressSwaggerSpec(spec);

        result.Should().Contain("id: integer *required");
        result.Should().Contain("email: string maxLength:255 *required");
    }

    [Fact]
    public void ExtractSchemas_ParsesComponentsSchemas()
    {
        var schemas = ApiSkillRoundHandler.ExtractSchemas(SampleSwaggerJson);

        schemas.Should().ContainKey("UserResponse");
        schemas.Should().ContainKey("UserListResponse");
        schemas.Should().ContainKey("CreateUserRequest");
    }

    [Fact]
    public void ExtractSchemas_InvalidJson_ReturnsEmpty()
    {
        var schemas = ApiSkillRoundHandler.ExtractSchemas("not json");
        schemas.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSchemaRef_ParsesRef()
    {
        var json = """{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Foo"}}}}""";

        var result = ApiSkillRoundHandler.ExtractSchemaRef(json);

        result.Should().Be("Foo");
    }

    [Fact]
    public void ExtractSchemaRef_NullInput_ReturnsNull()
    {
        ApiSkillRoundHandler.ExtractSchemaRef(null).Should().BeNull();
    }

    [Fact]
    public void ExtractResponseRefs_DeduplicatesContentTypes()
    {
        var json = """
            {
              "200": {
                "content": {
                  "application/json": { "schema": { "$ref": "#/components/schemas/Foo" } },
                  "text/plain": { "schema": { "$ref": "#/components/schemas/Foo" } },
                  "text/json": { "schema": { "$ref": "#/components/schemas/Foo" } }
                }
              }
            }
            """;

        var result = ApiSkillRoundHandler.ExtractResponseRefs(json);

        result.Should().HaveCount(1);
        result["200"].Should().Be("Foo");
    }

    [Fact]
    public void ExtractResponseRefs_MultipleStatusCodes()
    {
        var json = """
            {
              "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Ok" } } } },
              "404": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/NotFound" } } } }
            }
            """;

        var result = ApiSkillRoundHandler.ExtractResponseRefs(json);

        result.Should().HaveCount(2);
        result["200"].Should().Be("Ok");
        result["404"].Should().Be("NotFound");
    }

    [Fact]
    public void CompressSwaggerSpec_EndpointsShowRefNotFullJson()
    {
        var spec = new SwaggerSpec("Test", "v1",
            [
                new ApiEndpoint("POST", "/api/users", "CreateUser", [],
                    true,
                    """{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/CreateUserRequest"}}}}""",
                    """{"201":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/UserResponse"}}}}}""")
            ],
            [], SampleSwaggerJson);

        var result = ApiSkillRoundHandler.CompressSwaggerSpec(spec);

        result.Should().Contain("POST /api/users [auth]");
        result.Should().Contain("Request: CreateUserRequest");
        result.Should().Contain("201→UserResponse");
    }

    [Fact]
    public void ExtractSchemas_Swagger20Definitions()
    {
        var swagger2 = """
            {
              "swagger": "2.0",
              "definitions": {
                "Pet": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" },
                    "status": { "type": "string", "enum": ["available", "sold"] }
                  }
                }
              }
            }
            """;

        var schemas = ApiSkillRoundHandler.ExtractSchemas(swagger2);

        schemas.Should().ContainKey("Pet");
        schemas["Pet"].Should().Contain(s => s.Contains("enum[available, sold]"));
    }
}
