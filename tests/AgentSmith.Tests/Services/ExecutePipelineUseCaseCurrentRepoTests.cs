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
/// p0140d: ExecutePipelineUseCase.ResolveCurrentRepo resolves the run's RepoConnection
/// from PipelineRequest.RepoName + project.Repos and sets it on the PipelineContext
/// under ContextKeys.CurrentRepo before invoking the pipeline executor.
/// </summary>
public sealed class ExecutePipelineUseCaseCurrentRepoTests
{
    private readonly Mock<IConfigurationLoader> _configMock = new();
    private readonly Mock<IIntentParser> _intentMock = new();
    private readonly Mock<IPipelineExecutor> _pipelineMock = new();
    private readonly Mock<ISourceConfigOverrider> _sourceOverriderMock = new();
    private readonly ExecutePipelineUseCase _sut;

    public ExecutePipelineUseCaseCurrentRepoTests()
    {
        var skillLoaderMock = new Mock<ISkillLoader>();
        skillLoaderMock.Setup(s => s.LoadVocabulary(It.IsAny<string>()))
            .Returns(ConceptVocabulary.Empty);
        // SourceConfigOverrider is a no-op so it doesn't disturb CurrentRepo.
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
            NullLogger<ExecutePipelineUseCase>.Instance);
    }

    [Fact]
    public async Task ExecutePipeline_RepoNameNull_SingleRepoProject_FallsBackToOnlyRepo()
    {
        var onlyRepo = new RepoConnection { Name = "only-repo", Url = "https://example/only" };
        SetupConfig("demo", onlyRepo);
        var captured = CaptureCurrentRepo();
        var request = new PipelineRequest("demo", "fix-bug");

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().BeSameAs(onlyRepo);
    }

    [Fact]
    public async Task ExecutePipeline_RepoNameNull_MultiRepoProject_Throws()
    {
        SetupConfig("demo",
            new RepoConnection { Name = "repo-a" },
            new RepoConnection { Name = "repo-b" });
        var request = new PipelineRequest("demo", "fix-bug");

        var act = async () => await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("demo");
        ex.Which.Message.Should().Contain("RepoName");
    }

    [Fact]
    public async Task ExecutePipeline_RepoNameMatchesByNameCaseInsensitive()
    {
        var target = new RepoConnection { Name = "repo-a", Url = "https://example/a" };
        SetupConfig("demo", target, new RepoConnection { Name = "repo-b" });
        var captured = CaptureCurrentRepo();
        var request = new PipelineRequest("demo", "fix-bug", RepoName: "REPO-A");

        await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        captured.Value.Should().BeSameAs(target);
    }

    [Fact]
    public async Task ExecutePipeline_RepoNameNotInProjectRepos_Throws_WithKnownReposList()
    {
        SetupConfig("demo",
            new RepoConnection { Name = "a" },
            new RepoConnection { Name = "b" });
        var request = new PipelineRequest("demo", "fix-bug", RepoName: "bogus");

        var act = async () => await _sut.ExecuteAsync(request, "config.yml", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("bogus");
        ex.Which.Message.Should().Contain("a");
        ex.Which.Message.Should().Contain("b");
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

    private CapturedRepo CaptureCurrentRepo()
    {
        var captured = new CapturedRepo();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<string> _, ResolvedProject _, PipelineContext ctx, CancellationToken _) =>
            {
                captured.Value = ctx.Get<RepoConnection>(ContextKeys.CurrentRepo);
                return Task.FromResult(CommandResult.Ok("captured"));
            });
        return captured;
    }

    private sealed class CapturedRepo
    {
        public RepoConnection? Value { get; set; }
    }
}
