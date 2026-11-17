using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;
using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>2026-10-14-dbcel: the server's own composition resolves the requires links and the deriver with the carry it
/// runs before the model. 2026-11-17-5701b: a list's filing through the real composition is FilePipelineCompositionTests'.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class TicketGraphCompositionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;

    public TicketGraphCompositionTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    [Theory]
    [InlineData(TrackerType.AzureDevOps, typeof(AzureDevOpsRequiresLink))]
    [InlineData(TrackerType.Jira, typeof(JiraRequiresLink))]
    [InlineData(TrackerType.GitLab, typeof(GitLabRequiresLink))]
    [InlineData(TrackerType.GitHub, typeof(GitHubRequiresLink))]
    public void TicketProviderFactory_FromRealServiceCollection_BuildsRequiresLinkPerTrackerType(TrackerType type, Type expected)
    {
        var credentials = new Mock<ICredentialResolver>();
        credentials.Setup(c => c.For(It.IsAny<TrackerConnection>())).Returns("token");
        var services = Composition();
        services.AddSingleton(credentials.Object);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var tracker = new TrackerConnection
        {
            Name = "sample-tracker", Type = type, Organization = "sample-org", Project = "group/app",
            Url = type == TrackerType.GitHub ? "https://github.com/owner/repo" : "https://tracker.example", Email = "bot@example.com",
        };

        scope.ServiceProvider.GetRequiredService<ITicketProviderFactory>().CreateRequiresLink(tracker).Should().BeOfType(expected);
        scope.ServiceProvider.GetRequiredService<TicketGraphLinks>().Should().NotBeNull();
    }

    [Fact]
    public void SpecSetDeriver_FromRealServiceCollection_ResolvesTheMaterializer()
    {
        using var provider = Composition().BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISpecSetDeriver>().Should().BeOfType<SpecSetDeriver>();
        scope.ServiceProvider.GetRequiredService<ReferenceMaterializer>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IFilingRun>().Should().BeOfType<FilingRun>(); // 2026-11-17-5701b
    }

    private static ServiceCollection Composition()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        return services;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
