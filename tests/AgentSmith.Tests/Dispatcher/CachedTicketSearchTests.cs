using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Dispatcher;

public sealed class CachedTicketSearchTests : IDisposable
{
    private readonly Mock<IConfigurationLoader> _configLoader = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactory = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly CachedTicketSearch _sut;

    public CachedTicketSearchTests()
    {
        _sut = new CachedTicketSearch(
            _configLoader.Object,
            _ticketFactory.Object,
            _cache,
            NullLogger<CachedTicketSearch>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task SearchAsync_FirstCall_QueriesProvider()
    {
        SetupConfigWithProject("my-project");
        var ticketProvider = SetupTicketProvider("my-project",
        [
            CreateTicket(42, "Fix login bug"),
            CreateTicket(58, "Add dark mode")
        ]);

        var result = await _sut.SearchAsync("my-project", null);

        result.Should().HaveCount(2);
        ticketProvider.Verify(t => t.ListOpenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_SecondCall_ReturnsCached()
    {
        SetupConfigWithProject("my-project");
        var ticketProvider = SetupTicketProvider("my-project",
        [
            CreateTicket(42, "Fix login bug")
        ]);

        await _sut.SearchAsync("my-project", null);
        var result = await _sut.SearchAsync("my-project", null);

        result.Should().HaveCount(1);
        ticketProvider.Verify(t => t.ListOpenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithQuery_FiltersResults()
    {
        SetupConfigWithProject("my-project");
        SetupTicketProvider("my-project",
        [
            CreateTicket(42, "Fix login bug"),
            CreateTicket(58, "Add dark mode"),
            CreateTicket(99, "Login page redesign")
        ]);

        var result = await _sut.SearchAsync("my-project", "login");

        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Id == 42);
        result.Should().Contain(t => t.Id == 99);
    }

    [Fact]
    public async Task SearchAsync_UnknownProject_ReturnsEmpty()
    {
        SetupConfigWithProject("my-project");

        var result = await _sut.SearchAsync("unknown-project", null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ByTicketId_FiltersCorrectly()
    {
        SetupConfigWithProject("my-project");
        SetupTicketProvider("my-project",
        [
            CreateTicket(42, "Fix login bug"),
            CreateTicket(58, "Add dark mode")
        ]);

        var result = await _sut.SearchAsync("my-project", "42");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(42);
    }

    private void SetupConfigWithProject(string projectName)
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ProjectConfig>
            {
                [projectName] = new()
                {
                    Tickets = new TicketConfig { Type = "GitHub" }
                }
            }
        };

        _configLoader.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(config);
    }

    private Mock<ITicketProvider> SetupTicketProvider(string projectName, IReadOnlyList<Ticket> tickets)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.ListOpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tickets);

        _ticketFactory.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(provider.Object);

        return provider;
    }

    private static Ticket CreateTicket(int id, string title) =>
        new(new TicketId(id.ToString()), title, "Description", null, "Open", "github");
}
