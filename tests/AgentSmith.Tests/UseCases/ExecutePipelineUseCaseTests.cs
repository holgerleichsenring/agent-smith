using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.UseCases;

public class ExecutePipelineUseCaseTests
{
    private readonly Mock<IConfigurationLoader> _configMock = new();
    private readonly Mock<IIntentParser> _intentMock = new();
    private readonly Mock<IPipelineExecutor> _pipelineMock = new();
    private readonly Mock<ISourceConfigOverrider> _sourceOverriderMock = new();
    private readonly ExecutePipelineUseCase _sut;

    public ExecutePipelineUseCaseTests()
    {
        var skillLoaderMock = new Mock<ISkillLoader>();
        skillLoaderMock.Setup(s => s.LoadVocabulary(It.IsAny<string>()))
            .Returns(ConceptVocabulary.Empty);

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
            new AgentSmith.Application.Services.Lifecycle.RunCancellationRegistry(
                NullLogger<AgentSmith.Application.Services.Lifecycle.RunCancellationRegistry>.Instance),
            new AgentSmith.Application.Services.Claim.NoOpActiveRunLease(),
            NullLogger<ExecutePipelineUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_RunsPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = { ["todo-list"] = new ResolvedProject
            {
                Pipeline = "fix-bug",
                Repos = new[] { new RepoConnection { Name = "todo-list" } }
            } }
        };

        _configMock.Setup(c => c.LoadConfig("config.yml")).Returns(config);
        _intentMock.Setup(i => i.ParseAsync("fix #123 in todo-list", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("123"), new ProjectName("todo-list")));
        _pipelineMock.Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("Done"));

        var result = await _sut.ExecuteAsync("fix #123 in todo-list", "config.yml", false, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _pipelineMock.Verify(p => p.ExecuteAsync(
            It.Is<IReadOnlyList<string>>(cmds => cmds.Contains(CommandNames.FetchTicket)),
            It.IsAny<ResolvedProject>(),
            It.Is<PipelineContext>(ctx => ctx.Has(ContextKeys.TicketId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPipeline_IncludesPrUrl()
    {
        var config = new AgentSmithConfig
        {
            Projects = { ["todo-list"] = new ResolvedProject
            {
                Pipeline = "fix-bug",
                Repos = new[] { new RepoConnection { Name = "todo-list" } }
            } }
        };

        _configMock.Setup(c => c.LoadConfig("config.yml")).Returns(config);
        _intentMock.Setup(i => i.ParseAsync("fix #1 in todo-list", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("1"), new ProjectName("todo-list")));
        _pipelineMock.Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ResolvedProject>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<string> _, ResolvedProject _, PipelineContext ctx, CancellationToken _) =>
            {
                ctx.Set(ContextKeys.PullRequestUrl, "https://github.com/org/repo/pull/42");
                return Task.FromResult(CommandResult.Ok("Done"));
            });

        var result = await _sut.ExecuteAsync("fix #1 in todo-list", "config.yml", false, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.PrUrl.Should().Be("https://github.com/org/repo/pull/42");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProject_ThrowsConfigurationException()
    {
        var config = new AgentSmithConfig();
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
        _intentMock.Setup(i => i.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("1"), new ProjectName("unknown")));

        var act = () => _sut.ExecuteAsync("fix #1 in unknown", "config.yml", false, null, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*Project 'unknown' not found*");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPipeline_ThrowsConfigurationException()
    {
        var config = new AgentSmithConfig
        {
            Projects = { ["myproject"] = new ResolvedProject
            {
                Pipeline = "nonexistent",
                Repos = new[] { new RepoConnection { Name = "myproject" } }
            } }
        };
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
        _intentMock.Setup(i => i.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("1"), new ProjectName("myproject")));

        var act = () => _sut.ExecuteAsync("fix #1 in myproject", "config.yml", false, null, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*Pipeline 'nonexistent' not found*");
    }
}
