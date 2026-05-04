using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class ApiSkillRoundHandlerPrefixTests
{
    [Fact]
    public async Task ExecuteAsync_PrefixBuiltOncePerRound_AndSentSeparately()
    {
        var llmClient = new Mock<ILlmClient>();
        string? capturedPrefix = null;
        string? capturedSuffix = null;
        llmClient.Setup(c => c.CompleteWithCachedPrefixAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, TaskType, CancellationToken>(
                (_, p, s, _, _) => { capturedPrefix = p; capturedSuffix = s; })
            .ReturnsAsync(new LlmResponse("[]", 100, 50, "claude-sonnet-4-20250514"));

        var llmFactory = new Mock<ILlmClientFactory>();
        llmFactory.Setup(f => f.Create(It.IsAny<AgentConfig>())).Returns(llmClient.Object);

        var prefixBuilder = new PromptPrefixBuilder();
        var promptBuilder = new SkillPromptBuilder(prefixBuilder, new FakePromptCatalog(), new Mock<ISkillBodyResolver>().Object);
        var gateRetry = new GateRetryCoordinator(
            new GateOutputHandler(NullLogger<GateOutputHandler>.Instance),
            NullLogger<GateRetryCoordinator>.Instance);
        var handler = new ApiSkillRoundHandler(
            llmFactory.Object, promptBuilder, gateRetry,
            new UpstreamContextBuilder(),
            new StructuredOutputInstructionBuilder(new FakePromptCatalog()),
            new AgentSmith.Infrastructure.Services.ProjectBriefBuilder(),
            new NullBaselineLoader(),
            httpProbeRunner: null,
            logger: NullLogger<ApiSkillRoundHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineTypeName, PipelineType.Discussion);
        pipeline.Set(ContextKeys.SwaggerSpec, new SwaggerSpec(
            "Test", "1.0",
            [new ApiEndpoint("GET", "/api/x", null, [], false, null, null)],
            [], "{}"));
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles,
        [
            new() { Name = "anonymous-attacker", DisplayName = "Anon", Emoji = "👤", Rules = "test" }
        ]);

        var ctx = new ApiSecuritySkillRoundContext("anonymous-attacker", 1, new AgentConfig(), pipeline);
        await handler.ExecuteAsync(ctx, CancellationToken.None);

        capturedPrefix.Should().NotBeNull();
        capturedSuffix.Should().NotBeNull();
        capturedPrefix.Should().Contain("Swagger Specification");
        capturedSuffix.Should().Contain("Mode");
        capturedPrefix.Should().NotContain("## Mode");
    }
}
