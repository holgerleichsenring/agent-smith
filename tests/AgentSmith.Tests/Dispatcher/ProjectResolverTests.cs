using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Dispatcher;

public sealed class ProjectResolverTests
{
    private readonly Mock<IConfigurationLoader> _configMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly ProjectResolver _resolver;

    public ProjectResolverTests()
    {
        _resolver = new ProjectResolver(
            _configMock.Object,
            _ticketFactoryMock.Object,
            NullLoggerFactory.Instance.CreateLogger<ProjectResolver>());
    }

    [Fact]
    public async Task ResolveAsync_SingleProject_ReturnsProjectResolved()
    {
        SetupConfig(new Dictionary<string, ProjectConfig>
        {
            ["backend"] = new() { Tickets = new TicketConfig { Type = "github" } }
        });
        SetupTicketProviderExists();

        var result = await _resolver.ResolveAsync("42");

        result.Should().BeOfType<ProjectResolved>();
        ((ProjectResolved)result).ProjectName.Should().Be("backend");
    }

    [Fact]
    public async Task ResolveAsync_NoProjects_ReturnsProjectNotFound()
    {
        SetupConfig(new Dictionary<string, ProjectConfig>());

        var result = await _resolver.ResolveAsync("42");

        result.Should().BeOfType<ProjectNotFound>();
    }

    private void SetupConfig(Dictionary<string, ProjectConfig> projects)
    {
        _configMock.Setup(c => c.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig { Projects = projects });
    }

    private void SetupTicketProviderExists()
    {
        var ticket = new Ticket(new TicketId("42"), "Title", "Desc", null, "Open", "github");
        var providerMock = new Mock<ITicketProvider>();
        providerMock.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(providerMock.Object);
    }
}
