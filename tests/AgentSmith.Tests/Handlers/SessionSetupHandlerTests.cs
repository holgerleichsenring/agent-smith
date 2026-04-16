using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class SessionSetupHandlerTests
{
    private static readonly SwaggerSpec TestSpec = new(
        "Test API", "1.0", [],
        new List<SecurityScheme> { new("Bearer", "http", "header", "bearer") },
        "{}");

    [Fact]
    public async Task ExecuteAsync_WithNoCredentials_SetsPassiveMode()
    {
        var sessionProvider = new Mock<ISessionProvider>();
        var handler = new SessionSetupHandler(sessionProvider.Object,
            NullLogger<SessionSetupHandler>.Instance);

        var pipeline = new PipelineContext();
        var context = new SessionSetupContext(pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Passive mode");
        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var activeMode);
        activeMode.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_StoresTokensAndSetsActiveMode()
    {
        var sessionProvider = new Mock<ISessionProvider>();
        sessionProvider.Setup(s => s.AuthenticateAsync(
                It.IsAny<string>(), It.IsAny<SwaggerSpec>(),
                It.IsAny<PersonaCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token-123");

        var handler = new SessionSetupHandler(sessionProvider.Object,
            NullLogger<SessionSetupHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SwaggerSpec, TestSpec);
        pipeline.Set(ContextKeys.ApiTarget, "https://api.example.com");

        var personas = new Dictionary<string, PersonaCredentials>
        {
            ["user1"] = new() { Username = "user1", Password = "pass1" },
            ["admin"] = new() { Username = "admin", Password = "adminpass" }
        };
        pipeline.Set(ContextKeys.Personas, (object)personas);

        var context = new SessionSetupContext(pipeline);
        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Active mode");
        result.Message.Should().Contain("2 persona(s)");
        pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var activeMode);
        activeMode.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedAuth_FallsBackToPassive()
    {
        var sessionProvider = new Mock<ISessionProvider>();
        sessionProvider.Setup(s => s.AuthenticateAsync(
                It.IsAny<string>(), It.IsAny<SwaggerSpec>(),
                It.IsAny<PersonaCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new SessionSetupHandler(sessionProvider.Object,
            NullLogger<SessionSetupHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SwaggerSpec, TestSpec);
        pipeline.Set(ContextKeys.ApiTarget, "https://api.example.com");

        var personas = new Dictionary<string, PersonaCredentials>
        {
            ["user1"] = new() { Username = "user1", Password = "bad" }
        };
        pipeline.Set(ContextKeys.Personas, (object)personas);

        var context = new SessionSetupContext(pipeline);
        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Passive mode");
        result.Message.Should().Contain("failed: user1");
    }
}
