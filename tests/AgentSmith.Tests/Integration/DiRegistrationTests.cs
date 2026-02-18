using AgentSmith.Application;
using AgentSmith.Application.UseCases;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Integration;

public class DiRegistrationTests
{
    private readonly ServiceProvider _provider;

    public DiRegistrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ProcessTicketUseCase_IsResolvable()
    {
        var service = _provider.GetRequiredService<ProcessTicketUseCase>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void CommandExecutor_IsResolvable()
    {
        var service = _provider.GetRequiredService<ICommandExecutor>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void IntentParser_IsResolvable()
    {
        var service = _provider.GetRequiredService<IIntentParser>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void PipelineExecutor_IsResolvable()
    {
        var service = _provider.GetRequiredService<IPipelineExecutor>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurationLoader_IsResolvable()
    {
        var service = _provider.GetRequiredService<IConfigurationLoader>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AllFactories_AreResolvable()
    {
        _provider.GetRequiredService<ITicketProviderFactory>().Should().NotBeNull();
        _provider.GetRequiredService<ISourceProviderFactory>().Should().NotBeNull();
        _provider.GetRequiredService<IAgentProviderFactory>().Should().NotBeNull();
    }

    [Fact]
    public void CommandContextFactory_IsResolvable()
    {
        var service = _provider.GetRequiredService<ICommandContextFactory>();
        service.Should().NotBeNull();
    }
}
