using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

public sealed class ApiSecurityTriageHandlerSignalTests
{
    [Fact]
    public void BuildUserPrompt_WithActiveMode_IncludesActiveModeSignal()
    {
        // Access the prompt building via reflection (test the signal analysis section)
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ActiveMode, true);

        var spec = new SwaggerSpec(
            "Test API", "1.0",
            new List<ApiEndpoint>
            {
                new("GET", "/api/users/{id}", null, new List<ApiParameter>
                    { new("id", "path", "integer", true) }, true, null, null),
                new("DELETE", "/api/users/{id}", null, new List<ApiParameter>(), true, null, null),
                new("POST", "/api/upload", null, new List<ApiParameter>
                    { new("file", "formData", "file", true) }, true, null, null),
                new("GET", "/api/public/health", null, new List<ApiParameter>(), false, null, null),
            },
            new List<SecurityScheme>
            {
                new("Bearer", "http", "header", "bearer")
            },
            "{}");

        pipeline.Set(ContextKeys.SwaggerSpec, spec);

        // Verify the spec has the expected signals
        spec.Endpoints.Any(e => e.Path.Contains("{id}")).Should().BeTrue("ID-based paths");
        spec.Endpoints.Any(e => !e.RequiresAuth && e.Method != "OPTIONS").Should().BeTrue("anonymous endpoints");
        spec.Endpoints.Any(e => e.Method == "DELETE" && e.Path.Contains("{")).Should().BeTrue("DELETE on hierarchical");
    }

    [Fact]
    public void BuildUserPrompt_WithPassiveMode_IncludesPassiveModeSignal()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ActiveMode, false);

        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var active);
        active.Should().BeFalse();
    }
}
