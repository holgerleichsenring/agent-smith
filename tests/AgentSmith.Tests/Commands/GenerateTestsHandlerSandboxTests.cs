using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class GenerateTestsHandlerSandboxTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSandboxInPipelineContext_ThreadsSandboxToProvider()
    {
        var sandbox = Mock.Of<ISandbox>();
        var (handler, factory) = BuildHandler(out var capturedSandbox);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, sandbox);
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)Array.Empty<CodeChange>());

        await handler.ExecuteAsync(BuildContext(pipeline), CancellationToken.None);

        capturedSandbox().Should().BeSameAs(sandbox);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSandboxInPipelineContext_PassesNullToProvider()
    {
        var (handler, factory) = BuildHandler(out var capturedSandbox);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CodeChanges, (IReadOnlyList<CodeChange>)Array.Empty<CodeChange>());

        await handler.ExecuteAsync(BuildContext(pipeline), CancellationToken.None);

        capturedSandbox().Should().BeNull();
    }

    private static (GenerateTestsHandler Handler, Mock<IAgentProviderFactory> Factory) BuildHandler(
        out Func<ISandbox?> capturedSandbox)
    {
        ISandbox? captured = null;
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IProgressReporter>(), It.IsAny<ISandbox?>(), It.IsAny<CancellationToken>()))
            .Callback<Plan, Repository, string, string?, string?, IProgressReporter, ISandbox?, CancellationToken>(
                (_, _, _, _, _, _, sandbox, _) => captured = sandbox)
            .ReturnsAsync(new AgentExecutionResult([], null, null));
        var factoryMock = new Mock<IAgentProviderFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>())).Returns(providerMock.Object);
        capturedSandbox = () => captured;
        return (new GenerateTestsHandler(factoryMock.Object, Mock.Of<IProgressReporter>(),
            NullLogger<GenerateTestsHandler>.Instance), factoryMock);
    }

    private static GenerateTestsContext BuildContext(PipelineContext pipeline)
    {
        var changes = new List<CodeChange> { new(new FilePath("src/X.cs"), "x", "Modify") };
        var repo = new Repository("/tmp", new BranchName("main"), "https://github.com/o/r.git");
        return new GenerateTestsContext(repo, changes, "p", new AgentConfig { Type = "claude" }, pipeline);
    }
}
