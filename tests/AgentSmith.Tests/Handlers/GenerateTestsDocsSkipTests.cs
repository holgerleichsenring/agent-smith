using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

// p0355: SkipRepo_EmptyDiff_BuildTestDocSkipped — when no repo has a working-
// tree diff, the test/doc generation passes skip WITHOUT spending an LLM call,
// and say so honestly in the step result.
public sealed class GenerateTestsDocsSkipTests
{
    private readonly Mock<IChatClientFactory> _chatFactory = new(MockBehavior.Strict);

    private static PipelineContext PipelineWithCleanRepo()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => Task.FromResult(
                new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, null)));
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [new RepoConnection { Name = "server" }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["server"] = sandbox.Object });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, new Dictionary<string, string> { ["server"] = "server" });
        return pipeline;
    }

    private RepoDiffPartitioner NewPartitioner() =>
        new(new SandboxGitOperations(
                NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory()),
            NullLogger<RepoDiffPartitioner>.Instance);

    private static (Repository Repo, List<CodeChange> Changes) Inputs() =>
        (new Repository(new BranchName("fix/123"), "https://github.com/test/repo"),
            [new CodeChange(new FilePath("README.md"), "content", "Created")]);

    [Fact]
    public async Task SkipRepo_EmptyDiff_TestGenerationSkipped_NoLlmCall()
    {
        var (repo, changes) = Inputs();
        var handler = new GenerateTestsHandler(
            _chatFactory.Object, new AgentPromptBuilder(Mock.Of<IPromptCatalog>()),
            Mock.Of<IDecisionLogger>(), null, Mock.Of<IRunContextAccessor>(),
            NewPartitioner(), NullLogger<GenerateTestsHandler>.Instance);
        var context = new GenerateTestsContext(
            repo, changes, "principles", new AgentConfig(), PipelineWithCleanRepo());

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped");
        _chatFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SkipRepo_EmptyDiff_DocGenerationSkipped_NoLlmCall()
    {
        var (repo, changes) = Inputs();
        var handler = new GenerateDocsHandler(
            _chatFactory.Object, new AgentPromptBuilder(Mock.Of<IPromptCatalog>()),
            Mock.Of<IDecisionLogger>(), null, Mock.Of<IRunContextAccessor>(),
            NewPartitioner(), NullLogger<GenerateDocsHandler>.Instance);
        var context = new GenerateDocsContext(
            repo, changes, "principles", new AgentConfig(), PipelineWithCleanRepo());

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped");
        _chatFactory.VerifyNoOtherCalls();
    }
}
