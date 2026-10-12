using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-12-6bfae: the scope call failed with the provider's 404 and the step said
/// "Repo scoping skipped: single-repo run" — a sentence the gate files as silent.
/// </summary>
public sealed class ScopeReposCallFailureTests
{
    [Fact]
    public async Task ScopeRepos_ModelIdRejected_FailsTheStepWithTheRejection()
    {
        var rejection = new ModelIdRejectedException(
            "planning", "anthropic.claude-sonnet-5-5", "The anthropic provider rejected the model id", new Exception("404"));
        var executor = new CommandExecutor(Provider(Handler(rejection)), NullLogger<CommandExecutor>.Instance);

        var result = await executor.ExecuteAsync(Context("server"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be(rejection.Message);
    }

    [Fact]
    public async Task ScopeRepos_SingleRepoCallFailed_SaysSoAndIsNotANoOp()
    {
        var result = await Handler(new HttpRequestException("connection reset"))
            .ExecuteAsync(Context("server"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("scope call failed").And.Contain("connection reset");
        GateSilence.IsNoOpSummary(CommandNames.ScopeRepos, result.Message).Should().BeFalse();
    }

    [Fact]
    public void ScopeRepos_HealthySingleRepoSummary_StaysANoOp() =>
        GateSilence.IsNoOpSummary(CommandNames.ScopeRepos, "Repo scoping skipped: single-repo run (one repo or --repo)")
            .Should().BeTrue();

    private static IServiceProvider Provider(ScopeReposHandler handler)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ICommandHandler<ScopeReposContext>))).Returns(handler);
        return services.Object;
    }

    private static ScopeReposHandler Handler(Exception callFailure)
    {
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(callFailure);
        var factory = new Mock<IChatClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<AgentConfig>(), It.IsAny<TaskType>(), It.IsAny<int?>(), It.IsAny<MasterLoopHooks?>()))
            .Returns(chat.Object);
        var resolver = new Mock<ISandboxLanguageResolver>();
        resolver.Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteContextDiscovery("default", ".", "csharp")]);
        return new ScopeReposHandler(
            TestSupport.ApprovedSetDoubles.Scope(),
            new RemoteContextInventoryBuilder(resolver.Object, NullLogger<RemoteContextInventoryBuilder>.Instance),
            new RepoScopeClassifier(factory.Object, EventTestStubs.RunContext, NullLogger<RepoScopeClassifier>.Instance),
            new ScopeEstimateRecorder(AgentSmithConfig.Empty(), Mock.Of<AgentSmith.Contracts.Events.IEventPublisher>(),
                NullLogger<ScopeEstimateRecorder>.Instance),
            new ScopeRefusalRecorder(NullLogger<ScopeRefusalRecorder>.Instance),
            new ScopeNamedContextsRecorder(NullLogger<ScopeNamedContextsRecorder>.Instance),
            NullLogger<ScopeReposHandler>.Instance);
    }

    private static ScopeReposContext Context(params string[] repoNames)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            repoNames.Select(n => new RepoConnection { Name = n }).ToList());
        return new(
            new Ticket(new TicketId("42"), "Fix the API bug", "The server returns 500.", null, "open", "test"),
            new AgentConfig(), pipeline);
    }
}
