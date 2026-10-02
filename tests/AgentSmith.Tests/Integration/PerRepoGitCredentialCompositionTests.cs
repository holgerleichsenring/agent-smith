using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-02-5f89g: over the real CLI composition, two GitLab repos from two connections in one
/// run each clone and push with their own connection's token.
/// </summary>
public sealed class PerRepoGitCredentialCompositionTests
{
    private static readonly RepoConnection First = new()
    {
        Name = "api", Type = RepoType.GitLab, Url = "https://gitlab.one.example/team/api.git", Auth = "gitlab_one",
    };

    private static readonly RepoConnection Second = new()
    {
        Name = "web", Type = RepoType.GitLab, Url = "https://gitlab.two.example/team/web.git", Auth = "gitlab_two",
    };

    [Fact]
    public async Task TwoGitLabRepos_CloneAndPushWithTheirOwnTokens()
    {
        using var provider = Compose();
        var cloner = provider.GetRequiredService<SandboxRepoCloner>();
        var git = provider.GetRequiredService<SandboxGitOperations>();
        var (one, two) = (new RecordingSandbox(), new RecordingSandbox());

        await cloner.CheckoutIntoSandboxesAsync(First, null, [new("api", one)], CancellationToken.None);
        await cloner.CheckoutIntoSandboxesAsync(Second, null, [new("web", two)], CancellationToken.None);
        await git.CommitAndPushAsync(one, "b", "msg", First, CancellationToken.None);
        await git.CommitAndPushAsync(two, "b", "msg", Second, CancellationToken.None);

        one.TokensOf("clone", "push").Should().Equal("token-one", "token-one");
        two.TokensOf("clone", "push").Should().Equal("token-two", "token-two");
    }

    private static ServiceProvider Compose()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        services.AddSingleton(TestCredentials.Config(("gitlab_one", "token-one"), ("gitlab_two", "token-two")));
        services.RemoveAll<ISourceProviderFactory>();
        services.AddSingleton(RemoteSources());
        return services.BuildServiceProvider();
    }

    private static ISourceProviderFactory RemoteSources()
    {
        var source = new Mock<ISourceProvider>();
        source.SetupGet(p => p.ProviderType).Returns("GitLab");
        source.Setup(p => p.CheckoutAsync(It.IsAny<BranchName?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Repository(new BranchName("main"), "https://gitlab.example/team/x.git"));
        return Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == source.Object);
    }

    private sealed class RecordingSandbox : ISandbox
    {
        private readonly List<Step> _steps = [];

        public string JobId => "credential-test";

        public IEnumerable<string> TokensOf(params string[] verbs) =>
            _steps.Where(s => verbs.Any(v => s.Args?.Contains(v) == true))
                .Select(s => s.Env?.GetValueOrDefault("GIT_TOKEN") ?? "<none>");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            _steps.Add(step);
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.01,
                ErrorMessage: null, OutputContent: string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
