using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
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

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>2026-10-14-dbcel: the server's own composition files a list through the sink, and resolves the deriver
/// with the carry it runs before the model.</summary>
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

    [Fact]
    public async Task TicketFilingOutcomeSink_FromRealServiceCollection_FilesATwoTicketList()
    {
        var rig = new TicketGraphRig();
        var adapter = new Mock<IPlatformAdapter>();
        adapter.SetupGet(a => a.Platform).Returns("graph-test");
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(rig.Tracker);
        var approvals = new InMemorySpecApprovalStore();
        var services = Composition();
        services.AddSingleton(rig.Config()).AddSingleton(factory.Object).AddSingleton(adapter.Object)
            .AddSingleton<ITicketRequirementStore>(rig.Facts).AddSingleton<ITicketLandingStore>(rig.Facts)
            .AddSingleton<ISpecApprovalStore>(approvals).AddSingleton<IReferenceSetReader, NoReferenceSetReader>()
            .AddScoped(_ => new SpecDialogSessionRepository(_context));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        var state = await sessions.OpenAsync("graph-test", "C1", "th-1", "U1",
            new ActiveScope { Project = "proj", Repos = ["sample-api"] }, CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IOutcomeSink>()
            .AcceptAsync(state, List(Ticket("api"), Ticket("ui", "api")), true, CancellationToken.None);

        rig.Tracker.Created.Select(c => c.Title).Should().Equal("Title api", "Title ui");
        rig.Facts.Edges.Should().ContainSingle().Which.RequiredTicketId.Should().Be("101");
        (await approvals.GetAsync("sample-tracker", rig.KeyOf("102"), CancellationToken.None))!.TicketText.Should().BeTrue();
        adapter.Verify(a => a.SendInfoAsync("C1", It.IsAny<string>(),
            It.Is<string>(t => t.Contains("Filed a list of 2 tickets") && t.Contains("held: waits for #101")),
            "th-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SpecSetDeriver_FromRealServiceCollection_ResolvesTheMaterializer()
    {
        using var provider = Composition().BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISpecSetDeriver>().Should().BeOfType<SpecSetDeriver>();
        scope.ServiceProvider.GetRequiredService<ReferenceMaterializer>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TicketGraphFiler>().Should().NotBeNull();
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
