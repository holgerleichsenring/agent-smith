using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// ExecutePipelineUseCase publishes the run's repos on PipelineContext under
/// ContextKeys.Repos. Default: all configured project repos. When the request's
/// Context carries ContextKeys.SourceOverrideRepo (CLI `--repo NAME`), the list
/// is filtered to that single repo; unknown names throw with the known-repos list.
/// </summary>
public sealed class ExecutePipelineUseCaseReposTests
{
    private readonly Mock<IConfigurationLoader> _configMock = new();
    private readonly Mock<IIntentParser> _intentMock = new();
    private readonly Mock<IPipelineExecutor> _pipelineMock = new();
    private readonly Mock<ISourceConfigOverrider> _sourceOverriderMock = new();
    private readonly ExecutePipelineUseCase _sut;

    public ExecutePipelineUseCaseReposTests()
    {
        var skillLoaderMock = new Mock<ISkillLoader>();
        skillLoaderMock.Setup(s => s.LoadVocabulary(It.IsAny<string>()))
            .Returns(ConceptVocabulary.Empty);
        _sourceOverriderMock
            .Setup(o => o.Apply(It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>()))
            .Returns<ResolvedProject, PipelineContext>((project, _) => project);

        _sut = new ExecutePipelineUseCase(
            _configMock.Object,
            _intentMock.Object,
            _pipelineMock.Object,
            _sourceOverriderMock.Object,
            new StubSkillsCatalogResolver(),
            new StubSkillsCatalogPath(),
            skillLoaderMock.Object,
            new PipelineConfigResolver(),
            AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
            AgentSmith.Tests.TestHelpers.EventTestStubs.RunContext,
            new ModelPricingResolver(),
            NullLogger<ExecutePipelineUseCase>.Instance);
    }

    [Fact]
    public async Task ExecutePipeline_NoScopeOverride_PublishesAllConfiguredRepos()
    {
        var repoA = new RepoConnection { Name = "repo-a", Url = "https://example/a" };
        var repoB = new RepoConnection { Name = "repo-b", Url = "https://example/b" };
        SetupConfig("demo", repoA, repoB);
        var captured = CaptureRepos();
        var request = new PipelineRequest("demo", "fix-bug");

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().NotBeNull();
        captured.Value!.Should().BeEquivalentTo(new[] { repoA, repoB }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecutePipeline_RepoOverride_FiltersToSingleRepo_CaseInsensitive()
    {
        var target = new RepoConnection { Name = "repo-a", Url = "https://example/a" };
        SetupConfig("demo", target, new RepoConnection { Name = "repo-b" });
        var captured = CaptureRepos();
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object> { [ContextKeys.SourceOverrideRepo] = "REPO-A" });

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().ContainSingle().Which.Should().BeSameAs(target);
    }

    [Fact]
    public async Task ExecutePipeline_RepoOverride_UnknownName_Throws_WithKnownReposList()
    {
        SetupConfig("demo",
            new RepoConnection { Name = "a" },
            new RepoConnection { Name = "b" });
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object> { [ContextKeys.SourceOverrideRepo] = "bogus" });

        var act = async () => await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("bogus");
        ex.Which.Message.Should().Contain("a");
        ex.Which.Message.Should().Contain("b");
    }

    [Fact]
    public async Task ExecutePipeline_SourceFlags_RejectedOnMultiRepo_WithoutRepoOption()
    {
        SetupConfig("demo",
            new RepoConnection { Name = "a" },
            new RepoConnection { Name = "b" });
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object> { [ContextKeys.SourcePath] = "/tmp/repo" });

        var act = async () => await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("demo");
        ex.Which.Message.Should().Contain("--repo");
        ex.Which.Message.Should().Contain("a");
        ex.Which.Message.Should().Contain("b");
    }

    [Fact]
    public async Task ExecutePipeline_SourceFlags_WithRepoOption_AcceptedOnMultiRepo()
    {
        var target = new RepoConnection { Name = "a", Url = "https://example/a" };
        SetupConfig("demo", target, new RepoConnection { Name = "b" });
        var captured = CaptureRepos();
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object>
            {
                [ContextKeys.SourceOverrideRepo] = "a",
                [ContextKeys.SourcePath] = "/tmp/repo",
            });

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().ContainSingle().Which.Name.Should().Be("a");
    }

    [Fact]
    public async Task ExecutePipeline_SourceFlags_AcceptedOnSingleRepoProject_WithoutRepoOption()
    {
        var only = new RepoConnection { Name = "only", Url = "https://example/only" };
        SetupConfig("demo", only);
        var captured = CaptureRepos();
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object> { [ContextKeys.SourcePath] = "/tmp/repo" });

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecutePipeline_RepoOverride_OnSingleRepoProject_MatchingName_NoOp()
    {
        var only = new RepoConnection { Name = "only", Url = "https://example/only" };
        SetupConfig("demo", only);
        var captured = CaptureRepos();
        var request = new PipelineRequest("demo", "fix-bug",
            Context: new Dictionary<string, object> { [ContextKeys.SourceOverrideRepo] = "only" });

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().ContainSingle().Which.Should().BeSameAs(only);
    }

    private void SetupConfig(string projectName, params RepoConnection[] repos)
    {
        var project = new ResolvedProject
        {
            Name = projectName,
            Pipeline = "fix-bug",
            Repos = repos
        };
        var config = new AgentSmithConfig
        {
            Projects = { [projectName] = project }
        };
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
    }

    private CapturedRepos CaptureRepos()
    {
        var captured = new CapturedRepos();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<string> _, ResolvedProject _, PipelineContext ctx, CancellationToken _) =>
            {
                captured.Value = ctx.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
                return Task.FromResult(CommandResult.Ok("captured"));
            });
        return captured;
    }

    private sealed class CapturedRepos
    {
        public IReadOnlyList<RepoConnection>? Value { get; set; }
    }
}
