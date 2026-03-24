using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class GenerateTestsHandlerTests
{
    private readonly Mock<IAgentProviderFactory> _factoryMock = new();
    private readonly Mock<IProgressReporter> _reporterMock = new();
    private readonly GenerateTestsHandler _handler;

    public GenerateTestsHandlerTests()
    {
        _handler = new GenerateTestsHandler(
            _factoryMock.Object,
            _reporterMock.Object,
            NullLogger<GenerateTestsHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoChanges_SkipsTestGeneration()
    {
        var pipeline = new PipelineContext();
        var context = CreateContext(pipeline, []);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipping");
        _factoryMock.Verify(f => f.Create(It.IsAny<AgentConfig>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithChanges_CallsProviderAndMergesResults()
    {
        var existingChanges = new List<CodeChange>
        {
            new(new FilePath("src/Foo.cs"), "content", "Create")
        };
        var generatedTests = new List<CodeChange>
        {
            new(new FilePath("tests/FooTests.cs"), "test content", "Create")
        };
        SetupProvider(new AgentExecutionResult(generatedTests, null, null));

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)existingChanges);
        var context = CreateContext(pipeline, existingChanges);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 files changed");

        var merged = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        merged.Should().HaveCount(2);
        merged[0].Path.Value.Should().Be("src/Foo.cs");
        merged[1].Path.Value.Should().Be("tests/FooTests.cs");
    }

    [Fact]
    public async Task ExecuteAsync_ProviderReturnsNoChanges_DoesNotModifyPipeline()
    {
        var existingChanges = new List<CodeChange>
        {
            new(new FilePath("src/Foo.cs"), "content", "Create")
        };
        SetupProvider(new AgentExecutionResult([], null, null));

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)existingChanges);
        var context = CreateContext(pipeline, existingChanges);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges).Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_BuildsSyntheticPlanWithChangedFiles()
    {
        var changes = new List<CodeChange>
        {
            new(new FilePath("src/A.cs"), "a", "Create"),
            new(new FilePath("src/B.cs"), "b", "Modify")
        };
        Plan? capturedPlan = null;
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
            .Callback<Plan, Repository, string, string?, string?, IProgressReporter, CancellationToken>(
                (plan, _, _, _, _, _, _) => capturedPlan = plan)
            .ReturnsAsync(new AgentExecutionResult([], null, null));
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)changes);
        var context = CreateContext(pipeline, changes);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        capturedPlan.Should().NotBeNull();
        capturedPlan!.Steps.Should().HaveCount(1);
        capturedPlan.RawResponse.Should().Contain("src/A.cs");
        capturedPlan.RawResponse.Should().Contain("src/B.cs");
    }

    private void SetupProvider(AgentExecutionResult executionResult)
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IProgressReporter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);
    }

    private static GenerateTestsContext CreateContext(
        PipelineContext pipeline, IReadOnlyList<CodeChange> changes)
    {
        var repo = new Repository("/tmp", new BranchName("main"), "https://github.com/org/repo.git");
        return new GenerateTestsContext(
            repo, changes, "principles", new AgentConfig { Type = "claude" }, pipeline);
    }
}
