using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.UseCases;

public class ProcessTicketUseCaseTests
{
    private readonly Mock<IConfigurationLoader> _configMock = new();
    private readonly Mock<IIntentParser> _intentMock = new();
    private readonly Mock<IPipelineExecutor> _pipelineMock = new();
    private readonly ProcessTicketUseCase _sut;

    public ProcessTicketUseCaseTests()
    {
        _sut = new ProcessTicketUseCase(
            _configMock.Object,
            _intentMock.Object,
            _pipelineMock.Object,
            NullLogger<ProcessTicketUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ValidInput_RunsPipeline()
    {
        var config = new AgentSmithConfig
        {
            Projects = { ["todo-list"] = new ProjectConfig { Pipeline = "fix-bug" } },
            Pipelines = { ["fix-bug"] = new PipelineConfig { Commands = { "FetchTicketCommand" } } }
        };

        _configMock.Setup(c => c.LoadConfig("config.yml")).Returns(config);
        _intentMock.Setup(i => i.ParseAsync("fix #123 in todo-list", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("123"), new ProjectName("todo-list")));
        _pipelineMock.Setup(p => p.ExecuteAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ProjectConfig>(),
                It.IsAny<PipelineContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("Done"));

        var result = await _sut.ExecuteAsync("fix #123 in todo-list", "config.yml");

        result.IsSuccess.Should().BeTrue();
        _pipelineMock.Verify(p => p.ExecuteAsync(
            It.Is<IReadOnlyList<string>>(cmds => cmds.Contains("FetchTicketCommand")),
            It.IsAny<ProjectConfig>(),
            It.Is<PipelineContext>(ctx => ctx.Has(ContextKeys.TicketId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProject_ThrowsConfigurationException()
    {
        var config = new AgentSmithConfig();
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
        _intentMock.Setup(i => i.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("1"), new ProjectName("unknown")));

        var act = () => _sut.ExecuteAsync("fix #1 in unknown", "config.yml");

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*Project 'unknown' not found*");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPipeline_ThrowsConfigurationException()
    {
        var config = new AgentSmithConfig
        {
            Projects = { ["myproject"] = new ProjectConfig { Pipeline = "nonexistent" } }
        };
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
        _intentMock.Setup(i => i.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedIntent(new TicketId("1"), new ProjectName("myproject")));

        var act = () => _sut.ExecuteAsync("fix #1 in myproject", "config.yml");

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*Pipeline 'nonexistent' not found*");
    }
}
